// post-visual の並べ比べ（メニュー）: 持ち物・ツールチップ・ポーズ・設定・バグ報告・研究ツリー・チャレンジ一覧を Web と UITK で同じ状態にして 1280×720 で撮る
// post-visual comparison (menus): inventory, tooltip, pause, settings, bug report, research tree and challenge list in the same state on Web and UITK at 1280x720
// master と feature/uitk の両方でコンパイルできる API だけを使う（testid は両側にある main-slot・pause-menu-open-* だけ）
// Uses only APIs that compile on both branches (only testids present on both: main-slot, pause-menu-open-*)
using System;
using System.Reflection;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Overlay;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 400f };
return PlaytestRunner.Run("pv-menus", options, async p =>
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
    // 2. 持ち物に 3 種を入れて開く（参照の持ち物の形に近い数）
    // 2. Put three kinds into the inventory and open it
    await p.GiveItemDirect("原木", 7);
    await p.GiveItemDirect("石", 4);
    await p.GiveItemDirect("鉄インゴット", 32);
    await p.WaitSeconds(6f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.WaitSeconds(1.5f);
    await Shoot("4-player-inventory", true);
    await Shoot("5-japanese-inventory", true);

    // 3. 先頭の枠をホバーしたツールチップ
    // 3. Tooltip from hovering the first slot
    await p.HoverUi("main-slot-0");
    await p.WaitSeconds(1.5f);
    await Shoot("2-tooltip", false);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 4. 持ち物を多くの種類で埋めて開く
    // 4. Fill the inventory with many kinds and open it
    foreach (var name in new[] { "小石", "石器", "木の板", "木の棒", "砕いた石材", "粘土", "レンガ", "青銅インゴット", "木炭", "鉄鉱石", "鉄板", "石炭", "銅インゴット", "銅のワイヤー", "電子回路", "モーター" }) await p.GiveItemDirect(name, 10);
    await p.WaitSeconds(6f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.WaitSeconds(1.5f);
    await Shoot("4-player-inventory-full", true);
    await Shoot("4-item-list-large", true);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 5. ポーズ・設定・バグ報告
    // 5. Pause, settings and bug report
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    await p.UntilUiElement("pause-menu-open-settings", 15f);
    await p.WaitSeconds(1f);
    await Shoot("1-pause-top", true);
    await p.ClickUi("pause-menu-open-settings");
    await p.WaitSeconds(1.5f);
    await Shoot("1-settings", true);
    await p.PressKey(Key.Escape);
    await p.UntilUiElement("pause-menu-open-bug-report", 15f);
    await p.ClickUi("pause-menu-open-bug-report");
    await p.WaitSeconds(1.5f);
    await Shoot("1-bug-report", true);
    await p.PressKey(Key.Escape);
    await p.UntilUiElement("pause-menu-open-settings", 15f);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 6. 研究ツリー（R）とチャレンジ一覧（T）
    // 6. Research tree (R) and challenge list (T)
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.WaitSeconds(2f);
    await Shoot("4-research", true);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
    await p.WaitSeconds(2f);
    await Shoot("5-challenge-list", true);
    await p.PressKey(Key.Escape);
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
