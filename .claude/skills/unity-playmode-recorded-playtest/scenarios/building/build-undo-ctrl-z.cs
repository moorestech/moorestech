// 建築UI Ctrl+ZアンドゥのE2E検証
// 記録フックは PlaceSystemUtil.SendPlaceBlockProtocol(設置) と DragDeleteSelection.CommitDelete(撤去) のため、必ずUI経路で操作する（SendOnly直送は履歴に積まれず偽陰性になる）
// End-to-end check of the build-UI Ctrl+Z undo.
// Recording hooks live in PlaceSystemUtil.SendPlaceBlockProtocol (place) and DragDeleteSelection.CommitDelete (remove), so operate strictly via UI (direct SendOnly bypasses history = false negative).
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("build-undo-ctrl-z", options, async p =>
{
    // 足場+ワープ+無料設置
    // Ground + warp + free placement
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 設置カメラは北向き・浅いピッチのため、設置セルの南側に立つ
    // The placement camera faces north at a shallow pitch, so stand south of the target cell
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -1f));

    var blockName = "木のチェスト";
    var origin = new Vector3Int(2, 32, 4);
    var aimCenter = new Vector3(origin.x + 0.5f, 32.5f, origin.z + 0.5f);

    // Step 1
    p.Note("Step 1: ビルドメニュー→クリックで木のチェストを設置する");
    await p.PlaceBlockViaUi(blockName, origin, BlockDirection.North);
    await p.WaitBlockGameObject(origin);
    p.Assert(p.GetBlock(origin) != null, "Step1: UI設置でブロックが存在する");
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "Step1: 設置後もPlaceBlockモードに留まる（Ctrl+Z駆動の前提）");
    await p.Screenshot("01-placed");

    // Step 2
    p.Note("Step 2: Ctrl+Zで設置を取り消す（同座標同BlockIdの現存セルを撤去）");
    await PressCtrlZ(p);
    await p.Until(() => p.GetBlock(origin) == null, 15f, "Step2: Ctrl+Zでブロックが消滅する");
    p.Assert(p.GetBlock(origin) == null, "Step2: 設置Undo後にブロックが存在しない");
    await p.Screenshot("02-undo-place");

    // Step 3
    p.Note("Step 3: 再度UI設置してから破壊モード(G)でドラッグ撤去する");
    await p.PlaceBlockViaUi(blockName, origin, BlockDirection.North);
    await p.WaitBlockGameObject(origin);
    p.Assert(p.GetBlock(origin) != null, "Step3: 再設置でブロックが存在する");
    await p.Screenshot("03-replaced");

    // Gキー→破壊モード
    // G key -> destroy mode
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    p.Assert(p.CurrentUiState == UIStateEnum.DeleteBar, "Step3: Gキーで破壊モードへ遷移");

    // 照準→ドラッグ撤去
    // Aim, then drag-delete
    await DragDeleteAt(p, aimCenter);
    await p.Until(() => p.GetBlock(origin) == null, 15f, "Step3: ドラッグ撤去でブロックが消滅する");
    p.Assert(p.GetBlock(origin) == null, "Step3: ドラッグ撤去後にブロックが存在しない");
    // 再設置Undoの占有ガードはクライアントGameObjectを見るため、クライアント側の消滅を待つ
    // The re-place undo guard checks the client GameObject, so wait for the client-side despawn
    await p.WaitSeconds(1f);
    await p.Screenshot("04-drag-deleted");

    // Step 4
    p.Note("Step 4: Ctrl+Zで撤去を取り消す（占有範囲が空のセルを再設置）");
    await PressCtrlZ(p);
    await p.Until(() => p.GetBlock(origin) != null, 15f, "Step4: Ctrl+Zでブロックが再出現する");
    p.Assert(p.GetBlock(origin) != null, "Step4: 撤去Undo後にブロックが再び存在する");
    await p.WaitBlockGameObject(origin);
    await p.Screenshot("05-undo-remove");

    #region Internal

    // Ctrl+Zを注入する。HybridInputはLeftCtrl(GetKey保持)+Z(GetKeyDown)を見るため、
    // LeftCtrl押下→(保持したまま)Z押下→Z解放→LeftCtrl解放の順でQueueStateEventを送る
    // Inject Ctrl+Z. HybridInput checks LeftCtrl held (GetKey) plus Z (GetKeyDown),
    // so queue states in order: LeftCtrl down -> Z down (while held) -> Z up -> LeftCtrl up
    async UniTask PressCtrlZ(PlaytestDriver driver)
    {
        driver.Note("Ctrl+Z注入: LeftCtrl押下→Z押下→Z解放→LeftCtrl解放");
        SemanticInput.KeyDown(Key.LeftCtrl);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyDown(Key.Z);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.Z);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.LeftCtrl);
        await UniTask.DelayFrame(3);
        await driver.WaitSeconds(0.5f);
    }

    // 照準→押下→保持→解放で単体撤去
    // Aim, press, hold, release to delete one block
    async UniTask DragDeleteAt(PlaytestDriver driver, Vector3 worldCenter)
    {
        await driver.AimAt(worldCenter);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(10);
        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(3);
        await driver.WaitSeconds(0.5f);
    }

    #endregion
});
