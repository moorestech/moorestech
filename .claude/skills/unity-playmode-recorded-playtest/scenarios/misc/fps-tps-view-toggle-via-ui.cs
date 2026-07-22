// FPS/TPS視点切替E2E検証: Vキーでゲーム画面・建設モードのどこでも視点を切替え、建設中もカメラが変化しないことを確認する
// 検証項目: ゲーム画面でのV切替（FPS化・三人称復帰）→ 建設モード突入でカメラ距離/ピッチが不変（俯瞰化しない）→
// 三人称マウス照準設置 → 建設中のV切替とFPS中央照準設置 → 削除モード・ゲーム画面復帰後もFPSが維持される
// FPS/TPS view toggle E2E: toggle the view anywhere (game screen and build states) and verify the camera is
// untouched when entering build mode. Covers toggling on the game screen, unchanged camera distance/pitch on
// entering placement, third-person mouse-aim placement, FPS center-aim placement, and FPS memory across states.
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("fps-tps-view-toggle-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(4f, 33.5f, 5f));
    await p.PrepareBlockForUiPlacement("木のチェスト", 4);

    // 検証に使う実体への参照を確保する
    // Grab references to the objects under verification
    var camera = UnityEngine.Object.FindFirstObjectByType<InGameCameraController>();
    var viewModeController = ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlayerViewModeController>();
    var playerRenderers = UnityEngine.Object.FindFirstObjectByType<PlayerObjectController>().GetComponentsInChildren<Renderer>(true);
    var crosshairDot = CrosshairView.Instance.transform.Find("Dot").gameObject;
    System.Func<int> countBlocks = () => ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count;

    // 1: ゲーム画面の初期状態は三人称（クロスヘア無し・自機表示）
    // 1: The game screen starts in third-person (no crosshair, player visible)
    p.Note("ゲーム画面の初期状態が三人称であることを確認する");
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "GameScreenステート");
    p.Assert(viewModeController.GetCurrentMode() == PlayerViewMode.ThirdPerson, "初期視点はThirdPerson");
    p.Assert(!crosshairDot.activeInHierarchy, "クロスヘア非表示");
    p.Assert(playerRenderers.All(r => r.enabled), "自機Rendererが表示中");
    var thirdPersonDistance = camera.CameraDistance;
    await p.Screenshot("01-gamescreen-tps");

    // 2: 建設モードに入らず、ゲーム画面のままVでFPS化できる（今回の主題）
    // 2: V toggles to FPS on the game screen without entering any build state (the point of this change)
    p.Note("建設モードに入らず、ゲーム画面でVを押してFPSへ切り替える");
    await p.PressKey(Key.V);
    await p.Until(() => viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson, 5f, "ゲーム画面でFirstPersonへ切替");
    await p.Until(() => camera.CameraDistance < 0.2f, 5f, "カメラ距離がFPS距離(0.15)付近");
    p.Assert(Cursor.lockState == CursorLockMode.Locked, "カーソルがロックされている");
    p.Assert(crosshairDot.activeInHierarchy, "クロスヘアDotが表示中");
    p.Assert(playerRenderers.All(r => !r.enabled), "自機Rendererが全て非表示");
    await p.Screenshot("02-gamescreen-fps");

    // 3: ゲーム画面でVを再度押すと三人称へ戻り、元のカメラ距離が復元される
    // 3: Pressing V again on the game screen returns to third-person and restores the original distance
    p.Note("ゲーム画面でVを再度押し、三人称と元のカメラ距離へ戻ることを確認する");
    await p.PressKey(Key.V);
    await p.Until(() => viewModeController.GetCurrentMode() == PlayerViewMode.ThirdPerson, 5f, "ThirdPersonへ復帰");
    await p.Until(() => Mathf.Abs(camera.CameraDistance - thirdPersonDistance) < 0.05f, 5f, "元の三人称カメラ距離へ復元");
    p.Assert(!crosshairDot.activeInHierarchy, "クロスヘアが非表示に戻る");
    p.Assert(playerRenderers.All(r => r.enabled), "自機Rendererが再表示");
    await p.Screenshot("03-gamescreen-tps-restored");

    // 4: ビルドメニューを開いたままFPS/TPSを往復し、UIと三人称距離が維持される
    // 4: Toggle FPS/TPS while the build menu stays open, preserving the UI and third-person distance
    p.Note("ビルドメニューを開いたままVでFPS/TPSを切り替える");
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, "BuildMenuが表示中");
    await p.PressKey(Key.V);
    await p.Until(() => camera.CameraDistance < 0.2f, 5f, "BuildMenu表示中にFPS距離へ切替");
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, "FPS切替後もBuildMenuが表示中");
    await p.PressKey(Key.V);
    await p.Until(() => Mathf.Abs(camera.CameraDistance - thirdPersonDistance) < 0.05f, 5f, "BuildMenu表示中に三人称距離へ復帰");
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, "TPS復帰後もBuildMenuが表示中");
    await p.Screenshot("04-buildmenu-tps-restored");

    // 5: インベントリを開いたままFPS/TPSを往復し、UIと三人称距離が維持される
    // 5: Toggle FPS/TPS while the inventory stays open, preserving the UI and third-person distance
    p.Note("インベントリを開いたままVでFPS/TPSを切り替える");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    p.Assert(p.CurrentUiState == UIStateEnum.PlayerInventory, "PlayerInventoryが表示中");
    await p.PressKey(Key.V);
    await p.Until(() => camera.CameraDistance < 0.2f, 5f, "PlayerInventory表示中にFPS距離へ切替");
    p.Assert(p.CurrentUiState == UIStateEnum.PlayerInventory, "FPS切替後もPlayerInventoryが表示中");
    await p.PressKey(Key.V);
    await p.Until(() => Mathf.Abs(camera.CameraDistance - thirdPersonDistance) < 0.05f, 5f, "PlayerInventory表示中に三人称距離へ復帰");
    p.Assert(p.CurrentUiState == UIStateEnum.PlayerInventory, "TPS復帰後もPlayerInventoryが表示中");
    await p.Screenshot("05-inventory-tps-restored");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 6: 建設モードへ入ってもカメラ距離・ピッチが変化しない（俯瞰Tweenの廃止）
    // 6: Entering build mode leaves the camera distance and pitch untouched (the top-down tween is gone)
    p.Note("建設モードへ入る。カメラが俯瞰へ動かないことを確認する");
    var distanceBeforeBuild = camera.CameraDistance;
    var pitchBeforeBuild = camera.transform.eulerAngles.x;
    var mouseAimPos = new Vector3Int(6, 32, 7);
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("木のチェスト", mouseAimPos, BlockDirection.North));
    await p.OpenBuildMenuAndSelectBlock("木のチェスト");
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "PlaceBlockステートに遷移");
    await p.WaitSeconds(0.8f);
    p.Assert(Mathf.Abs(camera.CameraDistance - distanceBeforeBuild) < 0.05f, "建設モード突入でカメラ距離が変化しない");
    p.Assert(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x, pitchBeforeBuild)) < 2f, "建設モード突入でカメラピッチが変化しない（俯瞰化しない）");
    p.Assert(viewModeController.GetCurrentMode() == PlayerViewMode.ThirdPerson, "建設モードでも三人称のまま");
    p.Assert(Cursor.lockState != CursorLockMode.Locked, "三人称の建設モードはカーソル解放");
    await p.Screenshot("06-placeblock-tps-camera-unchanged");

    // 7: 三人称の建設モードでマウス照準設置ができる（回帰確認）
    // 7: Mouse-aim placement still works in the third-person build mode (regression)
    p.Note("三人称のままマウス照準でブロックを設置する");
    await p.AimAtPlaceOrigin("木のチェスト", mouseAimPos);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(mouseAimPos) != null, 10f, "三人称マウス照準でブロックが設置される");
    await p.Screenshot("07-tps-mouse-placed");

    // 8: 建設モード中にVでFPS化し、画面中央照準で設置できる
    // 8: Toggle FPS inside the build mode and place with the center aim
    p.Note("建設モード中にVでFPSへ切り替え、画面中央照準で設置する");
    await p.PressKey(Key.V);
    await p.Until(() => viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson, 5f, "建設モード中にFirstPersonへ切替");
    p.Assert(crosshairDot.activeInHierarchy, "FPS建設でクロスヘア表示");
    p.Assert(Cursor.lockState != CursorLockMode.Locked, "FPS建設でもUI側のカーソル解放を維持");
    var beforeFpsPlace = countBlocks();
    await p.ClickPlace();
    await p.Until(() => countBlocks() == beforeFpsPlace + 1, 10f, "FPS画面中央照準でブロックが設置される");
    await p.Screenshot("08-fps-center-placed");

    // 7: 削除モードへ遷移してもFPSが維持される
    // 7: FPS is remembered when moving to the delete state
    p.Note("Gで削除モードへ遷移し、FPSが維持されることを確認する");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    p.Assert(viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson, "削除モードでFPSが維持される");
    p.Assert(crosshairDot.activeInHierarchy, "削除モードでもクロスヘア表示");
    await p.Screenshot("09-deletebar-fps");

    // 8: 建設モードを抜けてゲーム画面へ戻ってもFPSのまま（旧仕様は三人称へ強制復帰していた）
    // 8: Leaving build mode keeps FPS on the game screen (the old behavior forced third-person back)
    p.Note("Gで建設モードを抜ける。ゲーム画面へ戻ってもFPSが維持されることを確認する");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.Assert(viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson, "ゲーム画面復帰後もFPSが維持される");
    p.Assert(camera.CameraDistance < 0.2f, "FPSカメラ距離が維持される");
    p.Assert(crosshairDot.activeInHierarchy, "ゲーム画面でもクロスヘア表示");
    p.Assert(playerRenderers.All(r => !r.enabled), "自機Rendererは非表示のまま");
    await p.Screenshot("10-gamescreen-fps-kept");

    // 9: FPS中にホットバーを持ち替えても、新しく生えた手持ちRendererが表示されない
    // 9: Swapping the hotbar during FPS must not leave the newly spawned grab-item renderers visible
    p.Note("FPS中にホットバーを持ち替え、手持ちアイテムが浮いて見えないことを確認する");
    await p.SelectHotbar(1);
    await p.WaitSeconds(0.5f);
    var renderersAfterHotbarSwap = UnityEngine.Object.FindFirstObjectByType<PlayerObjectController>().GetComponentsInChildren<Renderer>(true);
    p.Assert(renderersAfterHotbarSwap.All(r => !r.enabled), "持ち替え後も自機Rendererが全て非表示");
    await p.Screenshot("11-fps-hotbar-swap");

    // 10: Vで三人称へ戻して終了する
    // 10: Toggle back to third-person to finish
    p.Note("Vで三人称へ戻して終了する");
    await p.PressKey(Key.V);
    await p.Until(() => viewModeController.GetCurrentMode() == PlayerViewMode.ThirdPerson, 5f, "三人称へ復帰");
    await p.Until(() => 0.5f < camera.CameraDistance, 5f, "三人称カメラ距離へ復帰");
    p.Assert(playerRenderers.All(r => r.enabled), "自機Rendererが復帰");
    p.Assert(!crosshairDot.activeInHierarchy, "クロスヘアが非表示");
    await p.Screenshot("12-gamescreen-tps-final");
});
