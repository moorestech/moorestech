// post-visual の並べ比べ（スキット）: 会話の store を両側で同じ値に動かし、会話窓・選択肢・暗転・スキット中のポーズ・背景スキットを 1280×720 で撮る
// post-visual comparison (skit): drive the dialogue store with the same values on both sides and shoot text, choices, transition, pause-in-skit and background skit at 1280x720
// master と feature/uitk の両方でコンパイルできる API だけを使う（store の公開口は両側で同じ）
// Uses only APIs that compile on both master and feature/uitk (the store's public surface is the same on both)
using System;
using System.Reflection;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Overlay;
using Client.Skit.Skit;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 400f };
return PlaytestRunner.Run("pv-skit", options, async p =>
{
    // 1. 入力・言語・平地を両側で同じにする
    // 1. Align input, language and flat ground on both sides
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
    var store = SkitPresentationStateStore.Instance;

    // 2. 会話窓（Web の mock と同じ値）。打ち終わるまで待つ
    // 2. Dialogue window (same values as the Web mock); wait for typing to finish
    store.BeginBlocking(new SkitActionContext());
    store.SetTextAreaVisible(true);
    store.PresentBlockingText("Moore", "Blocking message");
    await p.WaitSeconds(2.5f);
    await Shoot("2-skit-text", true);

    // 3. 選択肢 2 つ
    // 3. Two choices
    store.PresentBlockingText("Moore", "Choose a route");
    await p.WaitSeconds(1.5f);
    store.PresentChoices(new[] { new SkitChoice { ChoiceId = "route-a", Label = "Route A" }, new SkitChoice { ChoiceId = "route-b", Label = "Route B" } });
    await p.WaitSeconds(1.5f);
    await Shoot("2-skit-choices", true);

    // 4. 暗転
    // 4. Transition fade
    store.SetTransitionVisible(true);
    await p.WaitSeconds(1f);
    await Shoot("2-skit-transition", true);
    store.SetTransitionVisible(false);

    // 5. スキット中のポーズ（会話は背後に残る）
    // 5. Pause during the skit (dialogue stays behind)
    store.PresentBlockingText("Moore", "Blocking message");
    await p.WaitSeconds(2.5f);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    store.SetInputSuspended(true);
    await p.WaitSeconds(1f);
    await Shoot("1-pause-in-skit", true);
    store.SetInputSuspended(false);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.WaitSeconds(0.5f);

    // 6. 背景スキット
    // 6. Background skit
    store.End();
    store.BeginBackground();
    store.SetBackgroundText("Moore", "Background message");
    await p.WaitSeconds(2f);
    await Shoot("2-skit-background", true);
    store.End();

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
