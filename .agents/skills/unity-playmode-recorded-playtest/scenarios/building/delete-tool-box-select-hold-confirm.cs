// 削除ツールの新方式: なぞり選択は離しても残り、F長押し(件数で0.75〜1.75秒)で確定。Shift+クリック2点の箱で範囲選択。ESCで全解除。Ctrl+Zで復元
// New delete-tool flow: stroke selection survives release, F hold (0.75-1.75s by count) confirms; Shift+click two-point box selects a range; ESC clears; Ctrl+Z restores
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using static Client.Playtest.Operations.Ui.DeleteBoxScenarioInput;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("delete-tool-box-select-hold-confirm", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -2f));

    // 準備: 木のチェストを一列に3つ直置き
    // Setup: place three wooden chests in a row directly
    var cells = new[] { new Vector3Int(2, 32, 4), new Vector3Int(4, 32, 4), new Vector3Int(6, 32, 4) };
    // 固定worldの既存物を混ぜず、空き場で元の件数を検証する
    // Verify the original count in a clear area without fixed-world fixtures
    for (var x = 1; x <= 11; x++)
    for (var z = 1; z <= 8; z++)
        p.Assert(p.GetBlock(new Vector3Int(x, 32, z)) == null, $"準備: fixture範囲({x},32,{z})は空いている");
    foreach (var cell in cells) PlaytestBlockOps.PlaceBlockDirect("木のチェスト", cell, BlockDirection.North);
    foreach (var cell in cells) await p.WaitBlockGameObject(cell);
    await p.Screenshot("01-prepared");

    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);

    // Step 1: なぞって離しても消えない
    // Step 1: a stroke that is released does not delete
    p.Note("Step 1: チェスト1をなぞって離す→赤いまま残り、消えない");
    await AimAtCell(p, cells[0]);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(10); SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(1.0f);
    p.Assert(p.GetBlock(cells[0]) != null, "Step1: 離しただけでは撤去されない");
    p.Assert(ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 1, $"Step1: ツールチップに選択件数1が出る 実際:{TooltipText()}");
    await p.Screenshot("02-stroke-kept");

    // 箱待ちだけを畳み、選択を残す。Fの対象はホバーでなく選択になる
    // Fold only the pending box, keeping the selection; F must target selection rather than hover
    await CheckEmptyBoxAndHeight(p, new Vector3Int(8, 32, 6), 1);
    await AimAtCell(p, cells[1]);

    // Step 2: F長押しで確定
    // Step 2: F hold confirms
    p.Note("Step 2: F長押し0.75秒（1件）で選択を削除");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(0.75f * 0.5f);
    await p.Screenshot("03-holding");
    await p.WaitSeconds(0.75f * 0.5f + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[0]) == null, 15f, "Step2: F長押し満了でチェスト1が撤去される");
    p.Assert(p.GetBlock(cells[1]) != null, "Step2: 選択があるとき別のホバー対象を消さない");
    await p.Screenshot("04-deleted");

    // Step 3: Shift+クリック2点の箱でチェスト2・3を選ぶ（終点確定だけでは消えない）
    // Step 3: box-select chests 2 and 3 with Shift+click then click; confirming the end alone deletes nothing
    p.Note("Step 3: Shift+クリックで始点、クリックで終点→範囲内2件");
    await BoxSelect(cells[1] + new Vector3Int(-1, 0, -1), cells[2] + new Vector3Int(1, 0, 1), 2, "Step3");
    p.Assert(p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, "Step3: 終点確定だけでは撤去されない");
    await p.Screenshot("06-box-selected");

    // Step 4: ESCで全解除→再び箱→F長押し
    // Step 4: ESC clears, box again, then F hold
    p.Note("Step 4: ESCで全解除（赤が戻る）→もう一度箱で選び直しF長押し");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.DeleteBar, "Step4: 選択があるときのESCは破壊モードに留まる");
    p.Assert(!HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount), $"Step4: 全解除で件数行が消える 実際:{TooltipText()}");
    await BoxSelect(cells[1] + new Vector3Int(-1, 0, -1), cells[2] + new Vector3Int(1, 0, 1), 2, "Step4");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(2f + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[1]) == null && p.GetBlock(cells[2]) == null, 15f, "Step4: 箱の2件がまとめて撤去される");
    await p.Screenshot("07-box-deleted");

    // Step 5: Ctrl+Zで2件が戻る
    // Step 5: Ctrl+Z restores both
    p.Note("Step 5: Ctrl+Zで箱の2件が1バッチで戻る");
    foreach (var (key, down) in new[] { (Key.LeftCtrl, true), (Key.Z, true), (Key.Z, false), (Key.LeftCtrl, false) })
    {
        if (down) SemanticInput.KeyDown(key);
        else SemanticInput.KeyUp(key);
        await UniTask.DelayFrame(3);
    }
    await p.Until(() => p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, 15f, "Step5: Ctrl+Zで2件とも復元される");
    await p.Screenshot("08-undone");

    // 押下中ESC後は離すまで新しいホバーを消さず、離した次の押下は単体撤去する
    // ESC during a hold blocks the new hover until release; the next hold deletes the hovered object
    await AimAtCell(p, cells[1]);
    await Click();
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(0.5f);
    await p.PressKey(Key.Escape);
    await AimAtCell(p, cells[2]);
    await p.WaitSeconds(2f + 0.3f);
    p.Assert(p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, "ESC後のF押し続けは選択もホバーも撤去しない");
    p.Assert(!HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount), "選択解除ESCで選択が空になる");
    SemanticInput.KeyUp(Key.F);
    await UniTask.DelayFrame(3);
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(2f + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[2]) == null, 15f, "空選択ならFでホバー1件を撤去する");
    p.Assert(p.GetBlock(cells[1]) != null, "ホバー以外のブロックを残す");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.Screenshot("09-hover-and-exit");

    #region Internal

    // Shift+クリックで始点、クリックで終点。終点待ち中の件数が expectedCount になることを確かめる
    // Shift+click the start, click the end; assert the pending count equals expectedCount
    async UniTask BoxSelect(Vector3Int startCell, Vector3Int endCell, int expectedCount, string label)
    {
        await AimAtGround(p, startCell);
        SemanticInput.KeyDown(Key.LeftShift);
        await UniTask.DelayFrame(3);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(5);
        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.LeftShift);
        await p.Until(() => GameObject.Find("DeleteBoxStartMarker") != null && GameObject.Find("DeleteBoxStartMarker").activeSelf, 5f, $"{label}: 始点マーカーが出る");
        await AimAtGround(p, endCell);
        await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange) == expectedCount, 5f, $"{label}: 範囲内の対象{expectedCount}件 実際:{TooltipText()}");
        await p.Screenshot($"05-{label}-box-end-hover");
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(5);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
