// FPS建設モードE2E検証: Vキーで俯瞰⇔FPSを切替えながら設置・削除モードを一巡する
// 検証項目: FPS化（カメラ距離・カーソルロック・クロスヘア・自機非表示）→ 画面中央照準の設置 →
// 俯瞰へ復帰してマウス照準設置（回帰）→ 削除モードへのFPS記憶維持 → 建設モード終了時の三人称復帰
// FPS build mode E2E: cycle place/delete states while toggling top-down <-> FPS with the V key.
// Verifies: FPS transition (camera distance, cursor lock, crosshair, hidden player model), center-aim
// placement, mouse-aim placement after returning to top-down (regression), FPS memory across the
// delete state, and third-person restoration when leaving build mode.
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.BuildView;
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

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("fps-build-mode-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(4f, 33.5f, 5f));
    await p.PrepareBlockForUiPlacement("木のチェスト", 4);

    // 検証に使う実体への参照を確保する
    // Grab references to the objects under verification
    var camera = UnityEngine.Object.FindFirstObjectByType<InGameCameraController>();
    var playerRenderers = UnityEngine.Object.FindFirstObjectByType<PlayerObjectController>().GetComponentsInChildren<Renderer>(true);
    var crosshairDot = CrosshairView.Instance.transform.Find("Dot").gameObject;
    System.Func<int> countBlocks = () => ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count;

    // 1: ビルドメニュー→ブロック選択→PlaceBlock遷移（俯瞰Tween）
    // 1: Build menu -> select block -> PlaceBlock transition (top-down tween)
    await p.OpenBuildMenuAndSelectBlock("木のチェスト");
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "PlaceBlockステートに遷移");
    p.Assert(AimPointProvider.CurrentMode == BuildViewMode.TopDown, "初期視点はTopDown");
    await p.Screenshot("01-topdown-place");

    // 2: V注入でFPS化を確認（距離0.15付近・照準モード・カーソルロック・クロスヘア・自機非表示）
    // 2: Inject V and verify FPS (distance ~0.15, aim mode, cursor lock, crosshair, hidden player)
    await p.PressKey(Key.V);
    await p.Until(() => AimPointProvider.CurrentMode == BuildViewMode.FirstPerson, 5f, "AimPointProviderがFirstPerson");
    await p.Until(() => camera.CameraDistance < 0.2f, 5f, "カメラ距離がFPS距離(0.15)付近");
    p.Assert(Cursor.lockState == CursorLockMode.Locked, "カーソルがロックされている");
    p.Assert(crosshairDot.activeInHierarchy, "クロスヘアDotが表示中");
    p.Assert(playerRenderers.All(r => !r.enabled), "自機Rendererが全て非表示");
    await p.Screenshot("02-fps-mode");

    // 3: 画面中央照準でブロック設置（クリック注入→サーバー側ブロック数の増加で確認）
    // 3: Place a block with the center aim (inject click, confirm via server block count)
    await p.AimAt(p.PlayerPosition + new Vector3(0f, -1f, 2f));
    var beforeFpsPlace = countBlocks();
    await p.ClickPlace();
    await p.Until(() => countBlocks() == beforeFpsPlace + 1, 10f, "FPS画面中央照準でブロックが設置される");
    await p.Screenshot("03-fps-placed");

    // 4: Vで俯瞰へ戻しマウス照準で設置できること（回帰確認）
    // 4: Toggle back to top-down and place with the mouse aim (regression)
    await p.PressKey(Key.V);
    await p.Until(() => AimPointProvider.CurrentMode == BuildViewMode.TopDown, 5f, "俯瞰へ復帰");
    p.Assert(Cursor.lockState != CursorLockMode.Locked, "カーソルロック解除");
    p.Assert(!crosshairDot.activeInHierarchy, "クロスヘア非表示");
    p.Assert(playerRenderers.All(r => r.enabled), "自機Rendererが再表示");

    // 俯瞰復帰Tween(0.25s)が整定してから照準する（照準とクリックの間にカメラが動くと投影がずれる）
    // Let the top-down restore tween (0.25s) settle before aiming (a moving camera skews the projection)
    await p.WaitSeconds(0.8f);
    var topDownPos = new Vector3Int(7, 32, 7);
    await p.AimAtPlaceOrigin("木のチェスト", topDownPos);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(topDownPos) != null, 10f, "俯瞰マウス照準でブロックが設置される");
    await p.Screenshot("04-topdown-placed-regression");

    // 5: FPSへ再切替してGで削除モードへ遷移し、FPS記憶が維持されることを確認
    // 5: Toggle FPS again, enter delete mode with G, and verify the FPS mode is remembered
    await p.PressKey(Key.V);
    await p.Until(() => AimPointProvider.CurrentMode == BuildViewMode.FirstPerson, 5f, "FPSへ再切替");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    p.Assert(AimPointProvider.CurrentMode == BuildViewMode.FirstPerson, "削除モードでFPSが維持される");
    p.Assert(crosshairDot.activeInHierarchy, "削除モードでもクロスヘア表示");
    await p.Screenshot("05-deletebar-fps");

    // 6: Gで建設モード終了→三人称カメラ復帰・自機再表示・クロスヘア非表示
    // 6: Exit build mode with G; third-person camera restored, player visible, crosshair hidden
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.Until(() => 0.5f < camera.CameraDistance, 5f, "三人称カメラ距離へ復帰");
    p.Assert(playerRenderers.All(r => r.enabled), "自機Rendererが復帰");
    p.Assert(!crosshairDot.activeInHierarchy, "クロスヘアが非表示");
    await p.Screenshot("06-exit-restored");
});
