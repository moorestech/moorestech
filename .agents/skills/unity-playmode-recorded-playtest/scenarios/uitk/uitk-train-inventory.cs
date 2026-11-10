// S4-train: UITK（GameUi）で駅の名前・積み降ろし切替と、機関車の持ち物・時刻表・自動運転を通す。Error ログ 0 件を主張する
// S4-train: on UITK (GameUi), run station rename and load/unload switch, then the locomotive's inventory, timetable and auto-run, asserting zero Error logs
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Train.TimetableEdit;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;
using TransferMode = Game.Block.Blocks.TrainRail.TrainPlatformTransferComponent.TransferMode;

return PlaytestRunner.Run("uitk-train-inventory", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f }, async p =>
{
    const string SelectedClass = "mode-switch__option--selected";
    Vector3Int stationAPos = new(10, 32, 4), stationBPos = new(10, 32, 64);
    var errorLogs = new List<string>();
    Application.logMessageReceived += CountErrors;
    try
    {
        p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, $"全体スイッチが UITK（{UiInputExclusivity.ActiveUiSystem}）");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
        await p.SetupFlatGround();
        await p.SkipOpeningSkitIfPlaying();
        await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
        Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
        // 駅 2 基を置いて結線する（駅 A の名前は既定名のまま）
        // Place two stations and link them (station A keeps its default name)
        foreach (var position in new[] { stationAPos, stationBPos }) { await p.PlaceBlockDirect("蒸気機関車駅", position, BlockDirection.North); await p.WaitBlockGameObject(position); }
        List<RailComponent> railsA = p.GetBlock(stationAPos).GetComponents<RailComponent>(), railsB = p.GetBlock(stationBPos).GetComponents<RailComponent>();
        RailComponent ExitOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
        RailComponent EntryOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
        await p.RunOnServer("駅2基を結線する", () => { ExitOf(railsA).FrontNode.ConnectNode(EntryOf(railsB).FrontNode); ExitOf(railsB).FrontNode.ConnectNode(EntryOf(railsA).FrontNode); });
        await p.Until(() => StationName() != null, 15f, "駅名の初期同期");
        var defaultName = StationName();
        // 1・2: 駅を開き、名前を打っただけでは送らず、適用で送る
        // 1-2: open the station; typing alone sends nothing, apply sends the name
        await OpenStation();
        await p.Screenshot("01-station");
        await p.SetText("train-station-name-input", "中央駅");
        await p.WaitSeconds(1f);
        p.Assert(StationName() == defaultName, $"打っただけでは駅名を送らない (name={StationName()})");
        await p.ClickUi("train-station-name-apply");
        await p.Until(() => StationName() == "中央駅", 10f, "クライアントの駅名が中央駅になる");
        await PlaytestUiElementOps.WaitUiTextContains("train-station-name-input", "中央駅", 10f);
        // 3: 入力欄にフォーカスがあるときの Esc・E での閉じを記録する（閉じないことを C2-b へ上げた。このレーンでは入力の振り分けを変えない）
        // 3: record closing by Esc and E while the text field has focus (not closing is raised to C2-b; this lane leaves input routing unchanged)
        foreach (var key in new[] { Key.Escape, Key.E })
        {
            p.Note($"フォーカス中の {key} で閉じたか: {await ClosesWhileFocused(key)}");
            if (p.CurrentUiState == UIStateEnum.SubInventory) await p.ClickUi("block-inventory-close");
            await p.WaitUiState(UIStateEnum.GameScreen, 10f);
            await OpenStation();
        }
        // 4: 卸しを押しても、クライアントの状態が替わるまで選択は替わらない
        // 4: pressing unload does not change the selection until the client state changes
        var selectedBeforeState = false;
        await PlaytestUiElementOps.ClickUi("train-platform-mode-unload");
        p.Assert(!Has("train-platform-mode-unload", SelectedClass) || Mode() == TransferMode.UnloadToPlatform, $"押した直後は状態より先に選択が替わらない (mode={Mode()})");
        await p.Until(() =>
        {
            if (Has("train-platform-mode-unload", SelectedClass) && Mode() != TransferMode.UnloadToPlatform) selectedBeforeState = true;
            return Mode() == TransferMode.UnloadToPlatform && Has("train-platform-mode-unload", SelectedClass);
        }, 10f, "クライアントのモードが卸しになり選択が替わる");
        p.Assert(!selectedBeforeState, "状態より先に選択が替わらない");
        await p.Screenshot("02-station-unload");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        // 機関車を駅 A の構内へ置き、持ち物の枠 0 に石を入れる
        // Place the locomotive inside station A and put stones in its slot 0
        var resolver = ClientDIContext.DIContainer.DIContainerResolver;
        var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => 0 < c.TractionForce);
        await p.RunOnServer("機関車を解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(carMaster.TrainCarGuid));
        await p.Until(() => resolver.Resolve<IGameUnlockStateData>().TrainCarUnlockStateInfos.TryGetValue(carMaster.TrainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロック同期");
        foreach (var required in carMaster.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var railPosition = new RailPosition(new List<IRailNode> { ExitOf(railsA).FrontNode, EntryOf(railsA).FrontNode }, TrainLengthConverter.ToRailUnits(carMaster.Length), 0);
        var placed = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(railPosition, carMaster.TrainCarGuid, default);
        p.Assert(placed != null && placed.Success, $"車両設置成功 (failure={placed?.FailureType})");
        await p.Until(() => SpawnedCar() != null, 15f, "車両entity出現");
        TrainUnit train = null; ItemTrainCarContainer container = null;
        await p.RunOnServer("列車を取得し枠 0 に石を入れる", () => (container = (ItemTrainCarContainer)(train = ServerContext.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First()).Cars[0].Container).SetItem(0, PlaytestItemOps.ResolveItemId("石"), 5));
        // 5: 機関車を開き、持ち物の枠を押して掴み・移動が機関車へ届くかを見る
        // 5: open the locomotive and press inventory slots to see grab and move reach the car
        var player = resolver.Resolve<IPlayerInventoryOperations>();
        await AimAtCar();
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.UntilUiElement("train-inventory-body", 10f);
        p.Assert(Has("train-tab-inventory", SelectedClass), "持ち物タブが選ばれている");
        await p.ClickUi("train-inventory-slots-slot-0");
        await p.Until(() => player.ReadGrab().Count == 5, 10f, "枠 0 の押下で石 5 個を掴む");
        await p.ClickUi("train-inventory-slots-slot-1");
        await p.Until(() => player.ReadGrab().IsEmpty, 10f, "枠 1 の押下で掴みが空になる");
        var movedCount = 0;
        await p.RunOnServer("機関車の枠 1 を読む", () => movedCount = container.GetItem(1).Count);
        p.Assert(movedCount == 5, $"石 5 個が機関車の枠 1 へ移る (count={movedCount})");
        await p.ClickUi("train-tab-timetable");
        await p.UntilUiElement("train-timetable-section", 10f);
        await p.ClickUi($"train-timetable-station-{stationAPos.x}_{stationAPos.y}_{stationAPos.z}-add");
        await p.WaitForTrainState(train, 1, null, null, 10f, "サーバーの時刻表が1駅になる");
        await p.Until(() => resolver.Resolve<IClientTrainTimetableLookup>().TryGet(train.TrainUnitInstanceId, out var snapshot) && snapshot.Stops.Count == 1, 10f, "クライアントの時刻表が1駅になる");
        await p.Until(() => !Has("train-timetable-section", "train-timetable-section--busy"), 10f, "保留が解ける");
        // 6: 自動運転を入れ、閉じたら編集の Model が使えない状態へ戻る
        // 6: turn auto-run on; closing returns the edit Model to unavailable
        await p.ClickUi("train-timetable-auto-run-on");
        await p.WaitForTrainState(train, null, true, null, 10f, "サーバーの自動運転がON");
        await p.Until(() => Has("train-timetable-auto-run-on", SelectedClass), 10f, "自動運転ONが選ばれる");
        await p.Screenshot("03-timetable-auto-run");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await p.Until(() => resolver.Resolve<ITrainTimetableEditModel>().Current.Value is UnavailableTrainTimetableArea, 10f, "閉じると時刻表の編集が使えない状態へ戻る");
    }
    finally
    {
        Application.logMessageReceived -= CountErrors;
    }

    // 7: 全体で Error ログが 0 件
    // 7: zero Error logs overall
    p.Assert(errorLogs.Count == 0, $"Error ログが 0 件 (count={errorLogs.Count}: {string.Join(" | ", errorLogs.Take(5))})");

    #region Internal
    void CountErrors(string condition, string stackTrace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorLogs.Add(condition); }
    string StationName() => ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(stationAPos).GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
    TransferMode? Mode() => ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(stationAPos).GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey)?.Mode;
    bool Has(string testid, string className) => GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid)?.ClassListContains(className) == true;

    // 入力欄を押してフォーカスを置き、キーを注入して 5 秒以内に閉じたかを返す
    // Click the field to focus it, inject the key and return whether the panel closed within 5 seconds
    async UniTask<bool> ClosesWhileFocused(Key key)
    {
        await p.ClickUi("train-station-name-input");
        p.Note($"{key} の前: フォーカス={GameUiRoot.Instance.GetRootElement().focusController.focusedElement?.GetType().Name} 文字入力中={UiInputExclusivity.IsTextInputFocused}");
        await p.PressKey(key);
        var deadline = Time.realtimeSinceStartup + 5f;
        while (p.CurrentUiState != UIStateEnum.GameScreen && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
        p.Note($"{key} の後: 状態={p.CurrentUiState} 文字入力中={UiInputExclusivity.IsTextInputFocused}");
        return p.CurrentUiState == UIStateEnum.GameScreen;
    }
    // 駅の外の南側からコライダーへ照準を合わせて開く（Task 8 の駅を開く手順と同じ）
    // Aim at the station collider from outside its south side and open it (same as Task 8's station opening)
    async UniTask OpenStation()
    {
        var colliders = new List<Collider>();
        await p.Until(() => 0 < (colliders = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(stationAPos).GetComponentsInChildren<Collider>(true)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.bounds.size != Vector3.zero).ToList()).Count, 10f, "駅のColliderが復帰する");
        var bounds = colliders[0].bounds;
        foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);
        p.WarpPlayer(new Vector3(bounds.center.x, 33.5f, bounds.min.z - 1f));
        await UniTask.DelayFrame(10);
        SemanticInput.KeyDown(Key.LeftAlt);
        await UniTask.DelayFrame(2);
        await p.AimAt(new Vector3(bounds.center.x, 32.4f, bounds.min.z + 0.1f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        foreach (var testid in new[] { "train-platform-view", "train-station-name-section", "train-platform-mode" }) await p.UntilUiElement(testid, 10f);
    }
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
    #endregion
});
