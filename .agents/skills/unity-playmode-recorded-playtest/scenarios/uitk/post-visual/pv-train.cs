// post-visual の並べ比べ（列車）: 地面の駅 2 基を結線して機関車を置き、持ち物タブ（空・物あり）・時刻表タブ・乗車中の HUD・乗車中のポーズを Web と UITK で同じ状態にして 1280×720 で撮る
// post-visual comparison (train): link two ground stations, place a locomotive and shoot the inventory tab (empty, filled), timetable tab, riding HUD and pause while riding, same state on Web and UITK at 1280x720
// 列車の組み立ては前例 uitk-s5-retake-train。master と feature/uitk の両方でコンパイルできる API だけを使う
// Train setup follows uitk-s5-retake-train; only APIs that compile on both master and feature/uitk
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using System.Reflection;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Overlay;
using Client.Playtest.Operations;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.UnlockState;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("pv-train", options, async p =>
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
    // 2. 地面の駅に機関車を置き、開いて持ち物タブ（空）を撮る
    // 2. Put a locomotive on a ground station, open it and shoot the empty inventory tab
    var train = await PlaceLocomotive();
    await AimAtCar();
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    await p.UntilUiElement("train-tab-timetable", 15f);
    await p.WaitSeconds(1.5f);
    await Shoot("4-block-trainTimetable", true);

    // 3. 枠 0・1 に物を入れた持ち物タブと、時刻表タブ
    // 3. The inventory tab with items in slots 0 and 1, then the timetable tab
    await p.RunOnServer("機関車の枠 0・1 に物を入れる", () => { var container = (ItemTrainCarContainer)train.Cars[0].Container; container.SetItem(0, PlaytestItemOps.ResolveItemId("原木"), 24); container.SetItem(1, PlaytestItemOps.ResolveItemId("石"), 8); });
    await p.WaitSeconds(2f);
    await Shoot("4-block-train", true);
    await p.ClickUi("train-tab-timetable");
    await p.WaitSeconds(1.5f);
    await Shoot("4-block-train-timetable-tab", true);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 4. 乗車中の HUD と、乗車中のポーズ
    // 4. The riding HUD and the pause while riding
    await AimAtCar();
    await p.PressRide();
    await p.Until(() => p.CurrentUiState == UIStateEnum.TrainHUDScreen, 15f, "列車 HUD の状態");
    await p.WaitSeconds(1.5f);
    await Shoot("2-train-hud", true);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    await p.WaitSeconds(1f);
    await Shoot("1-pause-in-train", true);
    await p.PressKey(Key.Escape);

    #region Internal

    // 地面の駅 2 基を結線し、駅 A の構内に機関車を置く（前例 uitk-s5-retake-train）
    // Link two ground stations and put a locomotive inside station A (precedent uitk-s5-retake-train)
    async UniTask<TrainUnit> PlaceLocomotive()
    {
        Vector3Int stationAPos = new(10, 32, 4), stationBPos = new(10, 32, 64);
        foreach (var position in new[] { stationAPos, stationBPos }) { await p.PlaceBlockDirect("蒸気機関車駅", position, BlockDirection.South); await p.WaitBlockGameObject(position); }
        List<RailComponent> railsA = p.GetBlock(stationAPos).GetComponents<RailComponent>(), railsB = p.GetBlock(stationBPos).GetComponents<RailComponent>();
        RailComponent ExitOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
        RailComponent EntryOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
        var master = MasterHolder.TrainUnitMaster.Train.TrainCars.First(car => 0 < car.TractionForce);
        await p.RunOnServer("駅 2 基を結線して機関車を解放する", () =>
        {
            ExitOf(railsA).FrontNode.ConnectNode(EntryOf(railsB).FrontNode); ExitOf(railsB).FrontNode.ConnectNode(EntryOf(railsA).FrontNode);
            EntryOf(railsB).BackNode.ConnectNode(ExitOf(railsA).BackNode); EntryOf(railsA).BackNode.ConnectNode(ExitOf(railsB).BackNode);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(master.TrainCarGuid);
        });
        foreach (var required in master.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var railPosition = new RailPosition(new List<IRailNode> { EntryOf(railsA).BackNode, ExitOf(railsA).BackNode }, TrainLengthConverter.ToRailUnits(master.Length), 0);
        var response = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(railPosition, master.TrainCarGuid, CancellationToken.None);
        p.Assert(response != null && response.Success, $"機関車を本番プロトコルで配置できる (failure={response?.FailureType})");
        await p.Until(() => SpawnedCar() != null, 15f, "機関車のクライアント表示");
        TrainUnit placed = null;
        await p.RunOnServer("列車を取得する", () => placed = ServerContext.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First());
        await p.WaitSeconds(6f);
        return placed;
    }

    // 後ろの車体の後端を狙う（前例 uitk-train-inventory の AimAtCar）
    // Aim at the rear end of the car body (precedent AimAtCar in uitk-train-inventory)
    async UniTask AimAtCar()
    {
        Collider rear = null;
        foreach (var collider in SpawnedCar().GetComponentsInChildren<Collider>(true)) if (rear == null || collider.bounds.min.z < rear.bounds.min.z) rear = collider;
        var bounds = rear.bounds;
        p.WarpPlayer(new Vector3(bounds.center.x, 33.5f, bounds.min.z - 1.2f));
        await p.WaitSeconds(1f);
        await p.AimAt(new Vector3(bounds.center.x, bounds.max.y - 0.3f, bounds.min.z + 0.3f));
    }

    TrainCarEntityObject SpawnedCar() => Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();

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
