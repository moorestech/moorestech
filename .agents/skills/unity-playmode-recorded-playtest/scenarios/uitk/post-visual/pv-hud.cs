// post-visual の並べ比べ（HUD・ホットバー・ビルドメニュー・配置・削除）: Web（master）と UITK（GameUi）で同じ状態を作り 1280×720 で撮る
// post-visual comparison (HUD, hotbar, build menu, placement, delete): build the same state on Web (master) and UITK (GameUi) and shoot at 1280x720
// master と feature/uitk の両方でコンパイルできる API だけを使う（PlaytestGameViewOps・Client.Ui は使わない）
// Uses only APIs that compile on both master and feature/uitk (no PlaytestGameViewOps or Client.Ui)
using System;
using System.Reflection;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Overlay;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 400f };
return PlaytestRunner.Run("pv-hud", options, async p =>
{
    // 1. 入力・言語・平地を両側で同じにする（Windows の PlayerPrefs の既定は English）
    // 1. Align input, language and flat ground on both sides (Windows PlayerPrefs default to English)
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "言語 japanese を当てられる");
    await SetResolution720();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.HideMapObjects();
    var backgroundSkit = Object.FindFirstObjectByType<BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
    await p.WaitSeconds(2f);

    // 2. 何も持たないゲーム画面（クロスヘア・空のホットバー）
    // 2. The plain game screen (crosshair, empty hotbar)
    await Shoot("2-game-hud", true);

    // 3. ホットバーの枠 0 に木のチェスト。割当の通知が消えるまで待つ
    // 3. Wooden chest in hotbar slot 0; wait for the assignment notification to leave
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 4), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 4));
    await p.WaitSeconds(6f);
    await Shoot("3-hotbar-equipment-empty", true);

    // 4. ビルドメニュー（最初のカテゴリ・ホバー無し）
    // 4. Build menu (first category, no hover)
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.WaitSeconds(1f);
    await Shoot("3-build-menu", true);

    // 5. ビルドメニューを閉じ、ホットバー枠 0 の木のチェストで空き地を狙った配置 HUD
    // 5. Close the build menu and aim at open ground holding the wooden chest from hotbar slot 0
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.Digit1);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    await p.AimAt(new Vector3(-2.5f, 32f, 4.5f));
    await p.WaitSeconds(1f);
    await Shoot("2-placement", false);
    await p.ExitToGameScreen();

    // 6. 削除モード（G）で置いたチェストを狙う
    // 6. Delete mode (G) aiming at the placed chest
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    await p.AimAt(new Vector3(2.5f, 32.5f, 4.5f));
    await p.WaitSeconds(1f);
    await Shoot("2-delete-bar", false);
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    #region Internal

    // 操作オーバーレイを消し、カーソルを外して撮る（moveCursor=false は照準中なので動かさない）
    // Hide the operation overlay, move the cursor away and shoot (moveCursor=false keeps the aim)
    async UniTask Shoot(string shotId, bool moveCursor)
    {
        foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        if (moveCursor) SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.8f);
        p.Assert(Screen.width == 1280 && Screen.height == 720, $"{shotId}: Screen が 1280x720 (実際 {Screen.width}x{Screen.height})");
        await p.Screenshot(shotId);
    }

    // Game View を 1280×720 の固定解像度にする（feature/uitk の GameViewResolution と同じ反射。master に無いので写す）
    // Set the Game View to fixed 1280x720 (same reflection as feature/uitk GameViewResolution, copied since master lacks it)
    async UniTask SetResolution720()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var asm = typeof(UnityEditor.Editor).Assembly;
        var (sizesType, sizeType) = (asm.GetType("UnityEditor.GameViewSizes"), asm.GetType("UnityEditor.GameViewSize"));
        var (kindType, gameViewType) = (asm.GetType("UnityEditor.GameViewSizeType"), asm.GetType("UnityEditor.GameView"));
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy).GetValue(null);
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new object[] { (int)sizesType.GetProperty("currentGroupType", flags).GetValue(sizes) });
        var total = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null);
        var index = -1;
        for (var i = 0; i < total && index < 0; i++)
        {
            var size = group.GetType().GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width", flags).GetValue(size) == 1280 && (int)sizeType.GetProperty("height", flags).GetValue(size) == 720 && sizeType.GetProperty("sizeType", flags).GetValue(size).ToString() == "FixedResolution") index = i;
        }
        if (index < 0)
        {
            var newSize = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) }).Invoke(new object[] { Enum.Parse(kindType, "FixedResolution"), 1280, 720, "1280x720" });
            group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { newSize });
            index = total;
        }
        var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
        gameViewType.GetMethod("SizeSelectionCallback", flags).Invoke(gameView, new object[] { index, null });
        await p.Until(() => Screen.width == 1280 && Screen.height == 720, 10f, "Screen が 1280x720 になる");
        await p.WaitSeconds(1f);
    }

    #endregion
});
