// 清浄機 8 台の部屋で清浄機のパネルを UITK（GameUi）で開き、Game View の高さ 1080・600 で部屋欄の末尾とフィルタ枠へ届き、パネルが下の安全帯へ潜らないことを通す（S4-machine Task 11 の録画送り。D-15）
// Opens the air-filter panel in a room of 8 purifiers on UITK (GameUi) and checks, at Game View heights 1080 and 600, the room list end and filter grid are reachable and the panel stays above the lower safe band (S4-machine Task 11 deferral, D-15)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

const int FilterCount = 8;
const float MenuLowerSafeArea = 70f; // Tokens.uss --menu-lower-safe-area
return PlaytestRunner.Run("uitk-cleanroom-airfilter-heights", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f }, async p =>
{
    // 実行中の Error ログを数えて最後に 0 件を主張する
    // Count error logs during the run and assert zero at the end
    var errorLogs = new List<string>();
    void CountErrors(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorLogs.Add(condition);
    }
    Application.logMessageReceived += CountErrors;
    try
    {
        p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, $"全体スイッチが UITK（UiInputExclusivity.ActiveUiSystem={UiInputExclusivity.ActiveUiSystem}）");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
        await p.SetupFlatGround();
        await p.SkipOpeningSkitIfPlaying();
        await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
        Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);

        // 1. x・z 2〜11 の床と天井をパネル、周りをブロックの壁で閉じ、北の壁沿いに清浄機を 8 台置く
        // 1. Close x/z 2..11 with panel floor and ceiling and block walls, and put 8 purifiers along the north wall
        foreach (var y in new[] { 32, 36 })
        foreach (var x in new[] { 2, 7 })
        foreach (var z in new[] { 2, 7 })
            await p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, y, z), BlockDirection.North);
        for (var y = 33; y <= 35; y++)
        for (var x = 2; x <= 11; x++)
        for (var z = 2; z <= 11; z++)
            if (x == 2 || x == 11 || z == 2 || z == 11) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
        IBlock firstFilter = null;
        for (var i = 0; i < FilterCount; i++)
        {
            var filter = await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(3 + i, 33, 10), BlockDirection.North);
            firstFilter ??= filter;
        }
        await p.WaitForCleanRoom(firstFilter, false, 15f, "部屋が検出された");

        // 2. 室内に立ち、清浄機を開く
        // 2. Stand inside and open a purifier
        p.WarpPlayer(new Vector3(6.5f, 33.5f, 8.6f));
        // 床パネルの板の上に立つ高さは 32.8 前後（地面 32 より上・天井より下）
        // Standing on the floor-panel plate puts the feet around 32.8 (above ground 32, below the ceiling)
        await p.Until(() => 32.5f < p.PlayerPosition.y && p.PlayerPosition.y < 33.2f, 5f, $"床パネルの上に着地（y={p.PlayerPosition.y:F2}）");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(6.5f, 33.6f, 10.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.Until(() => Root().Q<VisualElement>($"clean-room-air-filter-{FilterCount - 1}") != null && Root().Q<VisualElement>("clean-room-filter-grid") != null, 10f, $"部屋欄に清浄機 {FilterCount} 台とフィルタ枠");

        // 3. 高さ 1080（2560 でなく 1920 幅）と 600 で届くことを見る
        // 3. Check reachability at heights 1080 (1920 wide) and 600
        foreach (var (width, height) in new[] { (1920, 1080), (1280, 600) })
        {
            await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
            await UniTask.DelayFrame(5);
            p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}×{height}（実際 {Screen.width}×{Screen.height}）");
            await ExpectLayout($"{width}x{height}");
        }
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }
    finally
    {
        Application.logMessageReceived -= CountErrors;
    }

    // 4. 途中の Error ログが 0 件
    // 4. No error logs during the run
    p.Note($"Error ログ {errorLogs.Count} 件: {string.Join(" | ", errorLogs.Take(5))}");
    p.Assert(errorLogs.Count == 0, $"Error ログ 0 件 (count={errorLogs.Count})");

    #region Internal

    VisualElement Root() => GameUiRoot.Instance.GetRootElement();

    // CleanRoomLayoutTest と同じ主張: 安全帯より上・部屋欄の末尾とフィルタ枠が視口へ届く・入れ子の縦スクロールが無い
    // Same claims as CleanRoomLayoutTest: above the safe band, room end and filter grid reach the viewport, no nested vertical scroller
    async UniTask ExpectLayout(string label)
    {
        var panel = Root().Q<VisualElement>("block-inventory");
        var grid = Root().Q<VisualElement>("clean-room-filter-grid");
        var scroll = grid.GetFirstAncestorOfType<GameScrollView>();
        var scale = Root().panel.scaledPixelsPerPoint;
        p.Note($"{label}: 倍率={scale} 根の高さ={Root().worldBound.height} パネル下端={panel.worldBound.yMax} 大型={panel.ClassListContains("block-inventory--large")}");
        p.Assert(!panel.ClassListContains("block-inventory--large"), $"{label}: 清浄機のパネルは大型でない");
        p.Assert(panel.worldBound.yMax <= Root().worldBound.yMax - MenuLowerSafeArea + 0.5f, $"{label}: パネルの下端が下の安全帯より上（{panel.worldBound.yMax} ≤ {Root().worldBound.yMax - MenuLowerSafeArea}）");
        foreach (var target in new[] { Root().Q<VisualElement>($"clean-room-air-filter-{FilterCount - 1}"), grid })
        {
            scroll.ScrollTo(target);
            await UniTask.DelayFrame(3);
            var viewport = scroll.contentViewport.worldBound;
            p.Assert(target.worldBound.yMin >= viewport.yMin - 0.5f && target.worldBound.yMax <= viewport.yMax + 0.5f, $"{label}: {target.name} が視口へ届く（{target.worldBound.yMin}〜{target.worldBound.yMax} / 視口 {viewport.yMin}〜{viewport.yMax}）");
        }
        p.Assert(scroll.contentContainer.Q("clean-room-section") != null && scroll.GetFirstAncestorOfType<GameScrollView>() == null, $"{label}: 部屋欄とフィルタ枠が入れ子なしの同じ視口");
        await p.Screenshot($"airfilter-{label}");
    }

    #endregion
});
