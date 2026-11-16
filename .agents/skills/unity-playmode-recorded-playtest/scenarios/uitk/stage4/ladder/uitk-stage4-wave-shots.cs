// 段階4の波の撮影: 持ち物・機械・研究の同時に出る組を 1280x720 と 2560x1080 で撮る（録画しない）
// Stage-4 wave shots: capture the co-shown inventory, machine and research sets at 1280x720 and 2560x1080 (no recording)
// winunity play <branch> <このファイル> で流し、play/ に 6 枚を名前つきで撮る
// Run with winunity play <branch> <this file> and get the six named shots in play/
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 400f };
return PlaytestRunner.Run("uitk-stage4-wave-shots", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    // 裏タブの Game View は描画されず timeScale=0 のままスキットの Skip 送りが止まるので表へ出す（前例 uitk-opening-hud）
    // A Game View behind a tab never renders and skit skip stalls at timeScale=0, so bring it to front (precedent uitk-opening-hud)
    PlaytestGameViewOps.BringToFront();
    await p.Until(() => 0f < Time.timeScale, 10f, "Game View の描画が始まり timeScale が戻る");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // Game View を表へ出し、Story のままなら飛ばし直して Story を抜ける（回数は Driver が Note に残す）
    // Bring the Game View front and re-skip while in Story until it leaves (the Driver notes the rounds)
    await p.SkipOpeningSkitUntilLeftStory();
    await p.ExitToGameScreen();

    // 同じ持ち物と石窯を用意する
    // Prepare the same inventory and stone kiln
    await p.GiveItem("原木", 12);
    await p.GiveItem("青銅鉱石の粉", 4);
    await p.SetupFlatGround();
    var kilnPos = new Vector3Int(10, 32, 10);
    await p.PlaceBlockDirect("石窯", kilnPos, BlockDirection.North);
    var kilnObject = await p.WaitBlockGameObject(kilnPos);
    var kilnBounds = kilnObject.GetComponentsInChildren<Collider>(true)[0].bounds;
    // 持ち物を渡した通知が消えるまで待つ（並べ比べに写さない）
    // Wait for the item-given notifications to leave (keep them out of the comparison)
    await p.WaitSeconds(6f);

    // 2 つの解像度で 3 画面を撮る。2560x1080 に届かなければ SetResolution が例外で止め、720p の 3 枚は残る
    // Capture three screens at two resolutions; if 2560x1080 is unreachable SetResolution throws and the three 720p shots remain
    await ShootAll(1280, 720);
    await ShootAll(2560, 1080);

    // Game View の解像度は PlayMode の後も残るので、後に流すシナリオのために 1280x720 へ戻す（前例 uitk-gameview-resolution-switch）
    // The Game View resolution outlives PlayMode, so restore 1280x720 for later scenarios (as uitk-gameview-resolution-switch does)
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask ShootAll(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(1f);
        p.Note($"screen {Screen.width}x{Screen.height}");
        var suffix = $"{width}x{height}";

        // 持ち物
        // Player inventory
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
        await Shoot($"4-wave-playerInventory-{suffix}");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 石窯（機械）
        // Stone kiln (machine)
        p.WarpPlayer(new Vector3(kilnBounds.center.x, 33.5f, kilnBounds.min.z - 1.5f));
        await p.WaitSeconds(1f);
        await p.AimAt(kilnBounds.center);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await Shoot($"4-wave-subInventory-kiln-{suffix}");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 研究ツリー
        // Research tree
        await p.PressKey(Key.R);
        await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
        await Shoot($"4-wave-researchTree-{suffix}");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    // カーソルをパネル外へ逃がし、操作オーバーレイを消して落ち着かせてから撮る（前例 uitk-stage3-compare）
    // Move the cursor off panels, hide the operation overlay and settle before shooting (precedent uitk-stage3-compare)
    async UniTask Shoot(string shotId)
    {
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        await p.WaitSeconds(1.5f);
        await p.Screenshot(shotId);
    }

    #endregion
});
