// 時刻表UI経路の通し検証: 駅2基・レール・機関車を置き、Fで車両を開き時刻表タブから1駅ずつ追加し、時刻表に載るのを待つ→自動運転ON→走行→OFF→停止。
// 駅名はサーバー直で付け、時刻表タブの表示に名前が出ることを確認する（CEFへキー入力は転送されないため入力欄の打鍵はしない）。
// 時刻表の取得は列車インベントリを開いただけでは起きず、時刻表タブを選んだときに1回だけ起き、開き直すと再取得されることも確かめる。
// Timetable UI end-to-end: place two stations, rails and a locomotive, open the car with F, add the stations one at a time waiting until each lands in the timetable, turn auto-run on, watch it move, turn it off and watch it stop.
// Station names are set server-side and verified in the tab (keystrokes are not forwarded to CEF, so the name input is not typed).
// It also checks that opening the train inventory alone does not fetch the timetable, selecting the tab fetches it once, and reopening fetches it again.
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;
return PlaytestRunner.Run("train-timetable-via-ui", new PlaytestRunOptions { Record = true }, async p =>
{
    int mismatchCount = 0, rejectCount = 0, timetableRequestCount = 0;
    Game.Train.Unit.TrainUnit train = null;
    var followCar = false;
    Application.logMessageReceived += CountWarnings;
    try
    {
        // 背景Editorでも注入入力（F・クリック）が届くよう焦点無視へ切り替える
        // Ignore focus so injected input (F and clicks) reaches a background Editor
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
        await p.SetupFlatGround();
        p.WarpPlayer(new Vector3(16f, 33.5f, 10f));
        await p.SkipOpeningSkit();
        // スポーン付近の岩（マップオブジェクト）が駅の南端に重なり照準を遮るので消す
        // Hide map objects, since a rock near the spawn overlaps the station's south end and blocks the aim
        UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.MapObject.MapObjectGameObjectDatastore>().SetActive(false);
        // ===== 駅2基とレール =====
        // ===== Two stations and rails =====
        p.Note($"駅2基を直設置し、A出口→B入口、B出口→A入口を結線する（描画系={Client.Input.UiInputExclusivity.ActiveUiSystem}）");
        var stationAPos = new Vector3Int(10, 32, 4);
        var stationBPos = new Vector3Int(10, 32, 64);
        await p.PlaceBlockDirect("蒸気機関車駅", stationAPos, BlockDirection.South);
        await p.PlaceBlockDirect("蒸気機関車駅", stationBPos, BlockDirection.South);
        await p.WaitBlockGameObject(stationAPos);
        await p.WaitBlockGameObject(stationBPos);
        var stationA = p.GetBlock(stationAPos);
        var stationB = p.GetBlock(stationBPos);
        var railsA = stationA.GetComponents<RailComponent>();
        var railsB = stationB.GetComponents<RailComponent>();
        p.Assert(railsA.Count == 2 && railsB.Count == 2, "駅は2本のRailComponentを持つ");
        // UI の停車駅は back 側。駅を南向きに置くと back 側の進行が北向きになり、車両を back 側で A の停車位置に置けば反転せず北の B へ向かう（ConnectNode は片方向なので back 側も張る。docs/train/TrainSystemNotes.md）
        // UI stops use the back side; south-facing stations make back-side travel northward, so a car placed back-side at A's stop heads north to B without reversing (ConnectNode is one-way, so back edges are added too)
        RailComponent ExitOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
        RailComponent EntryOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
        await p.RunOnServer("駅2基を結線して命名する", () =>
        {
            ExitOf(railsA).FrontNode.ConnectNode(EntryOf(railsB).FrontNode);
            ExitOf(railsB).FrontNode.ConnectNode(EntryOf(railsA).FrontNode);
            EntryOf(railsB).BackNode.ConnectNode(ExitOf(railsA).BackNode);
            EntryOf(railsA).BackNode.ConnectNode(ExitOf(railsB).BackNode);
            stationA.GetComponent<TrainStationComponent>().SetStationName("北駅");
            stationB.GetComponent<TrainStationComponent>().SetStationName("南駅");
        });
        // ===== 機関車 =====
        // ===== Locomotive =====
        p.Note("機関車をアンロックし駅Aの構内へ設置、燃料投入");
        var resolver = ClientDIContext.DIContainer.DIContainerResolver;
        var clientUnlockState = resolver.Resolve<IGameUnlockStateData>();
        var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => 0 < c.TractionForce);
        await p.RunOnServer("機関車を解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(carMaster.TrainCarGuid));
        await p.Until(() => clientUnlockState.TrainCarUnlockStateInfos.TryGetValue(carMaster.TrainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロック同期");
        foreach (var required in carMaster.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var railPosition = new RailPosition(new List<IRailNode> { EntryOf(railsA).BackNode, ExitOf(railsA).BackNode }, TrainLengthConverter.ToRailUnits(carMaster.Length), 0);
        var placed = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(railPosition, carMaster.TrainCarGuid, CancellationToken.None);
        p.Assert(placed != null && placed.Success, $"車両設置成功 (failure={placed?.FailureType})");
        await p.Until(() => SpawnedCar() != null, 15f, "車両entity出現");
        p.SendCommand("addFuelToAllTrainCarsCommand");
        await p.WaitSeconds(1f);
        await p.RunOnServer("検証対象の列車を取得する", () => train = ServerContext.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First());
        var initial = await p.ReadTrainState(train);
        p.Assert(initial.Entries == 0 && !initial.AutoRun, "初期は時刻表が空で自動運転OFF");
        // ===== UI: 時刻表を設定 =====
        // ===== UI: configure the timetable =====
        p.Note("車両をFで開き時刻表タブへ");
        await AimAtCar();
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.UntilUiElement("train-tab-timetable", 10f);
        await p.WaitSeconds(1f);
        p.Assert(timetableRequestCount == 0, $"列車インベントリを開いただけでは時刻表を取得しない (count={timetableRequestCount})");
        await p.ClickUi("train-tab-timetable");
        await p.UntilUiElement("train-timetable-section", 10f);
        p.Assert(timetableRequestCount == 1, $"時刻表タブを選ぶと時刻表を1回取得する (count={timetableRequestCount})");
        await PlaytestUiElementOps.WaitUiTextContains("train-timetable-stations", "北駅", 10f);
        await PlaytestUiElementOps.WaitUiTextContains("train-timetable-stations", "南駅", 10f);
        await p.Screenshot("01-timetable-tab");
        p.Note("北駅・南駅を1駅ずつ追加（即時送信）");
        await p.ClickUi($"train-timetable-station-{stationAPos.x}_{stationAPos.y}_{stationAPos.z}-add");
        await p.WaitForTrainState(train, 1, null, null, 10f, "サーバーの時刻表が1駅になる");
        await p.Until(() => ClientTimetableStopCount() == 1, 10f, "クライアントの時刻表が1駅になる（保留が解ける）");
        await p.ClickUi($"train-timetable-station-{stationBPos.x}_{stationBPos.y}_{stationBPos.z}-add");
        await p.WaitForTrainState(train, 2, null, null, 10f, "サーバーの時刻表が2駅になる");
        await p.Until(() => ClientTimetableStopCount() == 2, 10f, "クライアントの時刻表が2駅になる");
        await p.UntilUiElement("train-timetable-stop-1", 5f);
        bool station0Matches = false, station1Matches = false;
        await p.RunOnServer("時刻表の停車駅を読む", () => { station0Matches = ReferenceEquals(train.trainDiagram.Entries[0].Node.StationRef.StationBlock, stationA); station1Matches = ReferenceEquals(train.trainDiagram.Entries[1].Node.StationRef.StationBlock, stationB); });
        p.Assert(station0Matches, "1駅目は北駅");
        p.Assert(station1Matches, "2駅目は南駅");
        await p.Screenshot("02-timetable-added");
        // ===== UI: 自動運転ON → 走行 =====
        // ===== UI: auto-run on → moving =====
        p.Note("自動運転ONで出発を待つ");
        await p.ClickUi("train-timetable-auto-run-on");
        await p.WaitForTrainState(train, null, true, null, 10f, "サーバーの自動運転がON");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        var before = SpawnedCar().transform.position;
        await p.WaitForTrainState(train, null, null, true, 15f, "列車が加速開始（1秒待機後に発車）");
        await p.WaitSeconds(3f);
        await p.Screenshot("03-running");
        var moved = (SpawnedCar().transform.position - before).magnitude;
        p.Assert(1f < moved, $"クライアント車両が動いた (moved={moved:F1})");
        // ===== UI: 自動運転OFF → 停止 =====
        // ===== UI: auto-run off → stopped =====
        // 走行中の車両は照準の間に離れるので、毎フレーム後端の南へ自機を置き直して手の届く距離を保ち、その場で後端へカーソルを合わせて開く
        // A moving car drifts away while aiming, so re-place the player behind it every frame to stay in reach and point the cursor at its rear on the spot
        p.Note("走行中の車両を追いながら開き直して自動運転OFF");
        followCar = true;
        FollowCar().Forget();
        await p.WaitSeconds(0.5f);
        for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.SubInventory; attempt++) { SemanticInput.MouseMoveTo(Camera.main.WorldToScreenPoint(RearAimPoint())); await p.PressInteract(); await p.WaitSeconds(1f); }
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.ClickUi("train-tab-timetable");
        await p.UntilUiElement("train-timetable-section", 10f);
        p.Assert(timetableRequestCount == 2, $"開き直して時刻表タブを選ぶと再取得する (count={timetableRequestCount})");
        await PlaytestUiElementOps.WaitUiTextContains("train-timetable-stops", "南駅", 10f);
        await p.ClickUi("train-timetable-auto-run-off");
        await p.WaitForTrainState(train, null, false, null, 10f, "サーバーの自動運転がOFF");
        await p.ClickUi("block-inventory-close");
        followCar = false;
        // 惰性の減速はごく緩く（実測: 120 秒後も 7.6）止まり切るのを待てないので、OFF 後に加速しないこと（減速）を主張する
        // Coasting decelerates very slowly (measured: still 7.6 after 120s), so assert it decelerates after OFF instead of waiting for a stop
        var speedAfterOff = (await p.ReadTrainState(train)).Speed;
        await p.WaitSeconds(3f);
        var speedLater = (await p.ReadTrainState(train)).Speed;
        p.Assert(speedLater < speedAfterOff, $"自動運転OFF後は惰性で減速する ({speedAfterOff:F2} → {speedLater:F2})");
        await p.Screenshot("04-coasting");
        p.Assert(mismatchCount == 0, $"Hash mismatch警告0件 (count={mismatchCount})");
        p.Assert(rejectCount == 0, $"時刻表・駅名プロトコルの拒否ログ0件 (count={rejectCount})");
        await p.Screenshot("final");
    }
    finally
    {
        followCar = false;
        Application.logMessageReceived -= CountWarnings;
    }
    #region Internal
    void CountWarnings(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("Hash mismatch detected")) mismatchCount++;
        // Web 系の拒否と UITK 系の拒否の Warning を同じ数え方で数え、両系で 0 件を主張する
        // Count Web-side and UITK-side rejection warnings the same way so both systems assert zero
        if (condition.Contains("[TrainScheduleEdit] rejected") || condition.Contains("[SetTrainStationName] rejected") || condition.Contains("[UiCommandResultToaster] train_")) rejectCount++;
        if (condition.Contains("[TrainTimetableFetcher] requesting timetable")) timetableRequestCount++;
    }
    // 車両は駅の構内にあり横から狙うと駅ブロックが先に当たるため、北向き固定カメラに合わせ駅の外の後端（南）側からFの届く2m以内（InteractTargetSelector.InteractDistance）で狙う
    // The car sits inside the station, so aiming from the side hits the station block first; aim northward from outside the rear (south) end within the 2m F reach
    async UniTask AimAtCar()
    {
        p.WarpPlayer(BehindRear());
        await p.WaitSeconds(1f);
        // 画面下端のHUDにカーソルが重なるとインタラクト走査が空になるため、車両の上寄りを狙う
        // Aim high on the car because a cursor over the bottom HUD empties the interact scan
        await p.AimAt(RearAimPoint());
        await p.WaitSeconds(0.5f);
    }
    Bounds RearBounds() => SpawnedCar().GetComponentsInChildren<Collider>(true).OrderBy(collider => collider.bounds.min.z).First().bounds;
    Vector3 RearAimPoint() { var rear = RearBounds(); return new(rear.center.x, rear.max.y - 0.3f, rear.min.z + 0.3f); }
    Vector3 BehindRear() { var rear = RearBounds(); return new(rear.center.x, 33.5f, rear.min.z - 1.2f); }
    async UniTaskVoid FollowCar() { while (followCar) { PlaytestSetup.WarpPlayer(BehindRear()); await UniTask.Yield(); } }
    // クライアントの時刻表の停車駅数（公開が届く前は 0）
    // Stop count of the client timetable (0 until the publish arrives)
    int ClientTimetableStopCount() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IClientTrainTimetableLookup>().TryGet(train.TrainUnitInstanceId, out var snapshot) ? snapshot.Stops.Count : 0;
    TrainCarEntityObject SpawnedCar() => UnityEngine.Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
    #endregion
});
