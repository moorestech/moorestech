// 削除ツール: F長押しの満了時間が選択件数で伸びること（5件=1.25秒・3件=0.75秒）と、Ctrl+クリック／Ctrl+Shift箱で選択から外せることを実操作で確かめる
// Delete tool: hold time grows with the selection count (5 = 1.25s, 3 = 0.75s), and Ctrl+click / Ctrl+Shift box removes targets from the selection, via production input
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.ProgressBar;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using static Client.Playtest.Operations.Ui.DeleteBoxScenarioInput;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

return PlaytestRunner.Run("delete-ctrl-deselect-and-hold-tiers", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    // 既存地形がカメラを覆わない上空へ足場とfixtureを揃える
    // Align the scaffold and fixture above terrain that would occlude the camera
    PlaytestSetup.CreateFlatGround().transform.position = new Vector3(0f, 198f, 0f);
    p.WarpPlayer(new Vector3(4.5f, 201.5f, -2f));
    var progress = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ProgressBarState>();

    // 準備: 木のチェストを5つ一列に直置き
    // Setup: place five wooden chests in a row directly
    var cells = new[] { new Vector3Int(2, 200, 4), new Vector3Int(4, 200, 4), new Vector3Int(6, 200, 4), new Vector3Int(8, 200, 4), new Vector3Int(10, 200, 4) };
    for (var x = 1; x <= 11; x++)
    for (var z = 1; z <= 8; z++)
        p.Assert(p.GetBlock(new Vector3Int(x, 200, z)) == null, $"準備: fixture範囲({x},200,{z})は空いている");
    foreach (var cell in cells)
    {
        PlaytestBlockOps.PlaceBlockDirect("木のチェスト", cell, BlockDirection.North);
        await p.WaitSeconds(0.5f);
    }
    foreach (var cell in cells) await p.WaitBlockGameObject(cell);
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);

    // Step 1: 箱で5件選ぶ
    // Step 1: box-select five chests
    p.Note("Step 1: Shift+クリック2点の箱で5件選ぶ");
    await Start(p, new Vector3Int(1, 200, 3));
    await AimAtGround(p, new Vector3Int(11, 200, 5));
    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange) == 5, 5f, $"Step1: 範囲内5件 実際:{TooltipText()}");
    await Click();
    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 5, 5f, $"Step1: 選択中5件 実際:{TooltipText()}");
    await p.Screenshot("01-five-selected");
    // 操作ログの被覆と本番ヒントの収まりを分けて記録する
    // Separate overlay occlusion from production hint layout
    var overlay = Object.FindFirstObjectByType<Client.Playtest.Overlay.PlaytestOverlayView>();
    overlay.enabled = false;
    await p.Screenshot("01b-key-hints-no-overlay");
    overlay.enabled = true;

    // Step 2: 5件は1.25秒なので1秒押しても消えない（離すと進捗は戻る）
    // Step 2: five targets take 1.25s, so a 1s hold deletes nothing (release resets progress)
    p.Note("Step 2: 5件選択でF長押し1秒→まだ消えない（5〜19件は1.25秒）");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(1.0f);
    p.Assert(progress.IsShown, "Step2: 長押し中は進捗バーが出る");
    foreach (var cell in cells) p.Assert(p.GetBlock(cell) != null, $"Step2: 1秒では{cell}を撤去しない");
    // 撮影IO待ちで保持時間を進めない
    // Freeze the hold clock while screenshot IO completes
    var savedTimeScale = Time.timeScale;
    Time.timeScale = 0f;
    p.Note("1秒時点を一時停止して撮影");
    await p.Screenshot("02-five-holding-1s");
    SemanticInput.KeyUp(Key.F);
    Time.timeScale = savedTimeScale;
    await p.WaitSeconds(0.3f);
    p.Assert(!progress.IsShown && ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 5, "Step2: 離すと進捗が消え選択は残る");

    // Step 3: Ctrl+クリックで1件外す
    // Step 3: Ctrl+click removes one target from the selection
    p.Note("Step 3: Ctrl+クリックでチェスト1を選択から外す");
    await AimAtCell(p, cells[0]);
    SemanticInput.KeyDown(Key.LeftCtrl);
    await UniTask.DelayFrame(3);
    await Click();
    SemanticInput.KeyUp(Key.LeftCtrl);
    // ホバー赤と区別するため照準を空き地へ外してから見る
    // Aim at empty ground first so the hover red does not mask the un-redded chest
    await AimAtGround(p, new Vector3Int(2, 200, 7));
    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 4, 5f, $"Step3: 選択中4件 実際:{TooltipText()}");
    await p.Screenshot("03-ctrl-click-deselected");

    // Step 4: Ctrl+Shift の箱でもう1件外す（青い箱・範囲内の選択済み件数）
    // Step 4: a Ctrl+Shift box removes another target (blue box, selected-in-range count)
    p.Note("Step 4: Ctrl+Shift+クリック2点の箱でチェスト2を選択から外す");
    await AimAtGround(p, new Vector3Int(3, 200, 3));
    SemanticInput.KeyDown(Key.LeftCtrl);
    SemanticInput.KeyDown(Key.LeftShift);
    await UniTask.DelayFrame(3);
    await Click();
    SemanticInput.KeyUp(Key.LeftShift);
    SemanticInput.KeyUp(Key.LeftCtrl);
    await AimAtGround(p, new Vector3Int(5, 200, 5));
    await p.Until(() => HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange) && ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange) == 1, 5f, $"Step4: 範囲内の選択済み1件 実際:{TooltipText()}");
    p.Assert(GameObject.Find("DeleteDeselectBoxRangeBox") != null && GameObject.Find("DeleteDeselectBoxRangeBox").activeSelf, "Step4: 除外の箱が表示される");
    await p.Screenshot("04-ctrl-shift-box");
    await Click();
    p.Assert(!Keyboard.current.leftCtrlKey.isPressed, "Step4: Ctrl解放後も除外の意図が保たれる");
    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 3, 5f, $"Step4: 選択中3件 実際:{TooltipText()}");

    // 未選択だけの箱は終点を拒否して既存選択を守る
    // Refuse a box containing only unselected targets and preserve the selection
    await AimAtGround(p, new Vector3Int(1, 200, 3));
    SemanticInput.KeyDown(Key.LeftCtrl);
    SemanticInput.KeyDown(Key.LeftShift);
    await UniTask.DelayFrame(3);
    await Click();
    SemanticInput.KeyUp(Key.LeftShift);
    SemanticInput.KeyUp(Key.LeftCtrl);
    await AimAtGround(p, new Vector3Int(3, 200, 5));
    await p.Until(() => HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxNoSelectedInRange), 5f, "除外対象0件の拒否理由が表示される");
    await Click();
    p.Assert(HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange) && ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 3, "0件終点を拒否して選択3件と箱待ちが残る");
    await p.Screenshot("04b-empty-deselect-refused");
    await p.PressKey(Key.Escape);

    // Step 5: 3件は0.75秒。外した2件は残り、選択中の3件だけ消える
    // Step 5: three targets take 0.75s; only the three selected are deleted and the two removed remain
    p.Note("Step 5: 3件選択でF長押し→0.75秒で3件だけ撤去、外した2件は残る");
    await AimAtCell(p, cells[0]);
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(0.5f);
    p.Assert(p.GetBlock(cells[2]) != null, "Step5: 0.5秒ではまだ撤去しない");
    await p.Until(() => p.GetBlock(cells[2]) == null && p.GetBlock(cells[3]) == null && p.GetBlock(cells[4]) == null, 15f, "Step5: 選択中の3件が撤去される");
    SemanticInput.KeyUp(Key.F);
    p.Assert(p.GetBlock(cells[0]) != null && p.GetBlock(cells[1]) != null, "Step5: 選択から外した2件は残る");
    await p.Screenshot("05-three-deleted");

    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
});
