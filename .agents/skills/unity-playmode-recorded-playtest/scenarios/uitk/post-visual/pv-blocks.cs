// post-visual の並べ比べ（ブロックの画面）: 平地に各種ブロックを直に置き、正面から開いた画面を Web と UITK で同じ状態にして 1280×720 で撮る
// post-visual comparison (block screens): place each block directly on the flat ground and shoot the panel opened from the front, same state on Web and UITK, at 1280x720
// master と feature/uitk の両方でコンパイルできる API だけを使う（開閉は UI 状態とキーで判定し testid に頼らない）
// Uses only APIs that compile on both branches (open/close is judged by UI state and keys, not testids)
using System;
using System.Reflection;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Overlay;
using Client.Playtest.Operations;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 900f };
return PlaytestRunner.Run("pv-blocks", options, async p =>
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
    // 2. 置く物の一覧（撮影 id・名前・原点・幅・奥行き）。1 列目は z=-20、2 列目は z=-5 に南向きの面をそろえる
    // 2. What to place (shot id, name, origin, width, depth); row 1 faces south at z=-20, row 2 at z=-5
    var targets = new (string Id, string Name, Vector3Int Origin, int Width, int Depth)[]
    {
        ("4-block-chest", "木のチェスト", new Vector3Int(-24, 32, -20), 1, 1),
        ("4-block-filterSplitter", "フィルター分岐器", new Vector3Int(-22, 32, -20), 1, 1),
        ("4-block-electricPole", "電柱", new Vector3Int(-20, 32, -20), 1, 1),
        ("4-block-gearPump", "歯車ポンプ", new Vector3Int(-18, 32, -20), 2, 2),
        ("4-block-gearMiner", "原始的な採掘機", new Vector3Int(-15, 32, -20), 2, 2),
        ("4-block-machine", "電気炉", new Vector3Int(-12, 32, -20), 3, 3),
        ("4-block-gearMachine", "原始的な加工機", new Vector3Int(-8, 32, -20), 3, 3),
        ("4-block-gearGenerator", "燃料式風車", new Vector3Int(-4, 32, -20), 3, 3),
        ("4-block-electricToGear", "回転生成機", new Vector3Int(0, 32, -20), 4, 4),
        ("4-block-miner", "電気採掘機", new Vector3Int(5, 32, -20), 3, 4),
        ("4-block-tank", "液体タンク", new Vector3Int(10, 32, -20), 9, 9),
        ("4-block-spaceElevator", "宇宙エレベーター拠点", new Vector3Int(-24, 32, -5), 9, 9),
        ("4-block-pumpNoVein", "油井", new Vector3Int(-13, 32, -5), 3, 9),
        ("4-block-generator", "ガソリンエンジン発電機", new Vector3Int(-8, 32, -5), 7, 13),
        ("4-block-trainPlatform", "貨物プラットフォーム", new Vector3Int(2, 32, -5), 7, 7),
        ("4-block-trainFluidPlatform", "液体プラットフォーム", new Vector3Int(11, 32, -5), 7, 7),
    };
    foreach (var target in targets) await p.PlaceBlockDirect(target.Name, target.Origin, BlockDirection.North);
    foreach (var target in targets) await p.WaitBlockGameObject(target.Origin);

    // 3. チェストの 2 枠に物を入れる（参照と同じ形）
    // 3. Put items into two chest slots (same shape as the reference)
    var chest = p.GetBlock(targets[0].Origin);
    await p.RunOnServer("チェストの 2 枠に物を入れる", () => { var inventory = chest.GetComponent<IOpenableBlockInventoryComponent>(); inventory.SetItem(0, PlaytestItemOps.ResolveItemId("原木"), 7); inventory.SetItem(1, PlaytestItemOps.ResolveItemId("石"), 4); });
    await p.WaitSeconds(3f);

    // 4. 1 つずつ正面から開いて撮り、Escape で閉じる。開けなければ記録して次へ
    // 4. Open each from the front, shoot, close with Escape; if it does not open, record and move on
    foreach (var target in targets)
    {
        if (!await TryOpen(target.Name, target.Origin, target.Width, target.Depth)) continue;
        await p.WaitSeconds(2f);
        await Shoot(target.Id, true);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #region Internal

    // 南面の手前に立ち Alt のカーソル照準で操作キーを押す（前例 uitk-machine-family-open）
    // Stand just south of the face and interact via the Alt cursor aim (precedent uitk-machine-family-open)
    async UniTask<bool> TryOpen(string name, Vector3Int origin, int width, int depth)
    {
        var center = origin.x + width / 2f;
        p.WarpPlayer(new Vector3(center, 32.5f, origin.z - 0.7f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, $"{name} の前に着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(center, 32.4f, origin.z + Mathf.Min(depth / 2f, 1.5f)));
        await p.PressInteract();
        var deadline = Time.realtimeSinceStartup + 5f;
        while (p.CurrentUiState != UIStateEnum.SubInventory && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
        SemanticInput.KeyUp(Key.LeftAlt);
        var opened = p.CurrentUiState == UIStateEnum.SubInventory;
        p.Assert(opened, $"{name}: 操作キーで開く（UI 状態={p.CurrentUiState}）");
        return opened;
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
