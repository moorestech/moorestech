// post-visual の並べ比べ（クリーンルーム）: 部屋を作り清浄機を 0→2→8→14→16→20→30 台と増やしながら、清浄機と室内の精密切断機の画面を Web と UITK で同じ状態にして 1280×720 で撮る
// post-visual comparison (clean room): build a room and grow purifiers 0->2->8->14->16->20->30, shooting the purifier and the in-room cutter panels in the same state on Web and UITK at 1280x720
// master と feature/uitk の両方でコンパイルできる API だけを使う（部屋の検出は両側にある WaitForCleanRoom）
// Uses only APIs that compile on both branches (room detection via WaitForCleanRoom, present on both)
using System;
using System.Reflection;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Overlay;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 900f };
return PlaytestRunner.Run("pv-cleanroom", options, async p =>
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
    // 2. x・z 2〜11 を床と天井のパネル・周りのブロックの壁で閉じ、室内の南西に精密切断機を置く（前例 uitk-cleanroom-airfilter-heights）
    // 2. Close x/z 2..11 with panel floor/ceiling and block walls, and put a precision cutter in the south-west of the room (precedent uitk-cleanroom-airfilter-heights)
    foreach (var y in new[] { 32, 36 })
    foreach (var x in new[] { 2, 7 })
    foreach (var z in new[] { 2, 7 })
        await p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, y, z), BlockDirection.North);
    for (var y = 33; y <= 35; y++)
    for (var x = 2; x <= 11; x++)
    for (var z = 2; z <= 11; z++)
        if (x == 2 || x == 11 || z == 2 || z == 11) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
    var machineOrigin = new Vector3Int(4, 33, 7);
    await p.PlaceBlockDirect("精密切断機", machineOrigin, BlockDirection.North);
    await p.WaitBlockGameObject(machineOrigin);

    // 3. 清浄機を置く順（プレイヤーに近い x=8 の列から）。立つ位置は精密切断機の南
    // 3. Purifier order (starting from the x=8 column near the player); the player stands south of the cutter
    var cells = new System.Collections.Generic.List<Vector3Int>();
    foreach (var z in new[] { 6, 7, 8, 9, 10, 5, 4, 3 }) cells.Add(new Vector3Int(8, 33, z));
    for (var z = 3; z <= 10; z++) cells.Add(new Vector3Int(9, 33, z));
    for (var z = 3; z <= 10; z++) cells.Add(new Vector3Int(10, 33, z));
    for (var x = 3; x <= 7; x++) cells.Add(new Vector3Int(x, 33, 10));
    cells.Add(new Vector3Int(3, 33, 4));
    var placed = 0;
    // 切断機は切断機の南から、清浄機は切断機に視線を遮られない清浄機の南西から開く
    // Open the cutter from its south and the purifier from its south-west where the cutter cannot block the view
    var (cutterStand, filterStand) = (new Vector3(5.5f, 33.5f, 5.4f), new Vector3(7.5f, 33.5f, 4.5f));
    IBlock firstFilter = null;

    // 4. 清浄機 0 台の精密切断機
    // 4. The cutter with no purifiers
    await OpenAndShoot(cutterStand, new Vector3(5.5f, 33.4f, 7.1f), "4-block-cleanRoomMachineNoFilters");

    // 5. 台数ごとに清浄機と精密切断機を撮る
    // 5. Shoot the purifier and the cutter at each count
    foreach (var (count, filterId, machineId) in new (int, string, string)[] { (2, "4-block-cleanRoomAirFilter2Filters", "4-block-cleanRoomMachine"), (8, "4-block-cleanRoomAirFilter8Filters", null), (14, "4-block-cleanRoomAirFilter14Filters", null), (16, null, "4-block-cleanRoomMachine16Filters"), (20, "4-block-cleanRoomAirFilter20Filters", "4-block-cleanRoomMachine20Filters"), (30, "4-block-cleanRoomAirFilter30Filters", "4-block-cleanRoomMachineManyFilters") })
    {
        for (; placed < count; placed++)
        {
            var filter = await p.PlaceBlockDirect("クリーンルーム空気清浄機", cells[placed], BlockDirection.North);
            if (firstFilter == null) firstFilter = filter;
        }
        await p.WaitBlockGameObject(cells[placed - 1]);
        await p.WaitForCleanRoom(firstFilter, false, 15f, $"清浄機 {count} 台の部屋が検出された");
        await p.WaitSeconds(2f);
        if (filterId != null) await OpenAndShoot(filterStand, new Vector3(8.5f, 33.5f, 6.5f), filterId);
        if (machineId != null) await OpenAndShoot(cutterStand, new Vector3(5.5f, 33.4f, 7.1f), machineId);
    }

    #region Internal

    // 室内の立つ位置に立ち、Alt のカーソル照準で狙った物を開いて撮り、Escape で閉じる
    // Stand at the given spot inside the room, open the aimed block via the Alt cursor, shoot, close with Escape
    async UniTask OpenAndShoot(Vector3 stand, Vector3 aim, string shotId)
    {
        p.WarpPlayer(stand);
        await p.Until(() => 32.5f < p.PlayerPosition.y && p.PlayerPosition.y < 33.2f, 5f, $"床パネルの上に着地（y={p.PlayerPosition.y:F2}）");
        SemanticInput.KeyDown(Key.LeftAlt);
        // 前の撮影で左端へ寄せたカーソルが Alt の照準になるので、画面の中央へ戻す
        // The cursor parked at the left edge by the previous shot becomes the Alt aim, so return it to the screen center
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        await p.AimAt(aim);
        await p.PressInteract();
        var deadline = Time.realtimeSinceStartup + 5f;
        while (p.CurrentUiState != UIStateEnum.SubInventory && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
        SemanticInput.KeyUp(Key.LeftAlt);
        var opened = p.CurrentUiState == UIStateEnum.SubInventory;
        p.Assert(opened, $"{shotId}: 操作キーで開く（UI 状態={p.CurrentUiState}）");
        if (!opened) return;
        await p.WaitSeconds(2f);
        await Shoot(shotId, true);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

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
