// 時刻表UI経路の通し検証: 駅2基・レール・機関車を置き、Fで車両を開き時刻表タブから2駅を追加→適用→自動運転ON→走行→OFF→停止。
// 駅名はサーバー直で付け、時刻表タブの表示に名前が出ることを確認する（CEFへキー入力は転送されないため入力欄の打鍵はしない）。
// Timetable UI end-to-end: place two stations, rails and a locomotive, open the car with F, add both stations from the
// timetable tab, apply, turn auto-run on, watch it move, turn it off and watch it stop. Station names are set server-side
// and verified in the tab (keystrokes are not forwarded to CEF, so the name input is not typed).
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
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
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("train-timetable-via-ui", options, async p =>
{
    var mismatchCount = 0;
    var rejectCount = 0;
    Application.logMessageReceived += CountWarnings;
    try
    {
        await p.SetupFlatGround();
        p.WarpPlayer(new Vector3(16f, 33.5f, 10f));
        await p.SkipOpeningSkit();

        // ===== 駅2基とレール =====
        // ===== Two stations and rails =====
        p.Note("駅2基を直設置し、A出口→B入口、B出口→A入口を結線する");
        var stationAPos = new Vector3Int(10, 32, 4);
        var stationBPos = new Vector3Int(10, 32, 64);
        p.PlaceBlockDirect("蒸気機関車駅", stationAPos, BlockDirection.North);
        p.PlaceBlockDirect("蒸気機関車駅", stationBPos, BlockDirection.North);
        await p.WaitBlockGameObject(stationAPos);
        await p.WaitBlockGameObject(stationBPos);
        var stationA = p.GetBlock(stationAPos);
        var stationB = p.GetBlock(stationBPos);
        var railsA = stationA.GetComponents<RailComponent>();
        var railsB = stationB.GetComponents<RailComponent>();
        p.Assert(railsA.Count == 2 && railsB.Count == 2, "駅は2本のRailComponentを持つ");
        // Exit(front)→相手Entry(front) を張ると逆方向 back 側も自動で張られる（docs/train/TrainSystemNotes.md）
        // Connecting exit(front)→other entry(front) also creates the reverse back-side edge (docs/train/TrainSystemNotes.md)
        RailComponent ExitOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
        RailComponent EntryOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
        ExitOf(railsA).FrontNode.ConnectNode(EntryOf(railsB).FrontNode);
        ExitOf(railsB).FrontNode.ConnectNode(EntryOf(railsA).FrontNode);
        stationA.GetComponent<TrainStationComponent>().SetStationName("北駅");
        stationB.GetComponent<TrainStationComponent>().SetStationName("南駅");

        // ===== 機関車 =====
        // ===== Locomotive =====
        p.Note("機関車をアンロックし駅Aの構内へ設置、燃料投入");
        var resolver = ClientDIContext.DIContainer.DIContainerResolver;
        var clientUnlockState = resolver.Resolve<IGameUnlockStateData>();
        var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => 0 < c.TractionForce);
        p.ServerService<IGameUnlockStateDataController>().UnlockTrainCar(carMaster.TrainCarGuid);
        await p.Until(() => clientUnlockState.TrainCarUnlockStateInfos.TryGetValue(carMaster.TrainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロック同期");
        foreach (var required in carMaster.RequiredItems) p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var trainLength = TrainLengthConverter.ToRailUnits(carMaster.Length);
        var exitA = ExitOf(railsA).FrontNode;
        var entryA = EntryOf(railsA).FrontNode;
        var railPosition = new RailPosition(new List<IRailNode> { exitA, entryA }, trainLength, 0);
        var placed = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(railPosition, carMaster.TrainCarGuid, CancellationToken.None);
        p.Assert(placed != null && placed.Success, $"車両設置成功 (failure={placed?.FailureType})");
        await p.Until(() => SpawnedCar() != null, 15f, "車両entity出現");
        p.SendCommand("addFuelToAllTrainCarsCommand");
        await p.WaitSeconds(1f);
        var train = p.ServerService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First();
        p.Assert(train.trainDiagram.Entries.Count == 0 && !train.IsAutoRun, "初期は時刻表が空で自動運転OFF");

        // ===== UI: 時刻表を設定 =====
        // ===== UI: configure the timetable =====
        p.Note("車両をFで開き時刻表タブへ");
        await AimAtCar();
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.ClickWebUi("train-tab-timetable");
        await p.UntilWebUiElement("train-timetable-section", 10f);
        await PlaytestWebUiOps.WaitWebUiTextContains("train-timetable-stations", "北駅", 10f);
        await PlaytestWebUiOps.WaitWebUiTextContains("train-timetable-stations", "南駅", 10f);
        await p.Screenshot("01-timetable-tab");

        p.Note("北駅・南駅を追加して適用");
        await p.ClickWebUi($"train-timetable-station-{stationAPos.x}_{stationAPos.y}_{stationAPos.z}-add");
        await p.ClickWebUi($"train-timetable-station-{stationBPos.x}_{stationBPos.y}_{stationBPos.z}-add");
        await p.UntilWebUiElement("train-timetable-stop-1", 5f);
        await p.ClickWebUi("train-timetable-apply");
        await p.Until(() => train.trainDiagram.Entries.Count == 2, 10f, "サーバーの時刻表が2駅になる");
        p.Assert(ReferenceEquals(train.trainDiagram.Entries[0].Node.StationRef.StationBlock, stationA), "1駅目は北駅");
        p.Assert(ReferenceEquals(train.trainDiagram.Entries[1].Node.StationRef.StationBlock, stationB), "2駅目は南駅");
        await p.Screenshot("02-timetable-applied");

        // ===== UI: 自動運転ON → 走行 =====
        // ===== UI: auto-run on → moving =====
        p.Note("自動運転ONで出発を待つ");
        await p.ClickWebUi("train-timetable-auto-run-on");
        await p.Until(() => train.IsAutoRun, 10f, "サーバーの自動運転がON");
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        var before = SpawnedCar().transform.position;
        await p.Until(() => 0.0 < train.CurrentSpeed, 15f, "列車が加速開始（1秒待機後に発車）");
        await p.WaitSeconds(3f);
        await p.Screenshot("03-running");
        var moved = (SpawnedCar().transform.position - before).magnitude;
        p.Assert(1f < moved, $"クライアント車両が動いた (moved={moved:F1})");

        // ===== UI: 自動運転OFF → 停止 =====
        // ===== UI: auto-run off → stopped =====
        p.Note("車両を開き直して自動運転OFF");
        await AimAtCar();
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.ClickWebUi("train-tab-timetable");
        await p.ClickWebUi("train-timetable-auto-run-off");
        await p.Until(() => !train.IsAutoRun, 10f, "サーバーの自動運転がOFF");
        await p.ClickWebUi("block-inventory-close");
        await p.Until(() => train.CurrentSpeed <= 0.0, 30f, "惰性停止");
        await p.Screenshot("04-stopped");

        p.Assert(mismatchCount == 0, $"Hash mismatch警告0件 (count={mismatchCount})");
        p.Assert(rejectCount == 0, $"時刻表・駅名プロトコルの拒否ログ0件 (count={rejectCount})");
        await p.Screenshot("final");
    }
    finally
    {
        Application.logMessageReceived -= CountWarnings;
    }

    #region Internal

    void CountWarnings(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("Hash mismatch detected")) mismatchCount++;
        if (condition.Contains("[TrainScheduleEdit] rejected") || condition.Contains("[SetTrainStationName] rejected")) rejectCount++;
    }

    // 車両は駅の構内にあり横から狙うと駅ブロックが先に当たるため、北向き固定カメラに合わせ駅の外の後端（南）側からFの届く2m以内（InteractTargetSelector.InteractDistance）で狙う
    // The car sits inside the station, so aiming from the side hits the station block first; aim northward from outside the rear (south) end within the 2m F reach
    async UniTask AimAtCar()
    {
        Collider rear = null;
        foreach (var collider in SpawnedCar().GetComponentsInChildren<Collider>(true))
        {
            if (rear == null || collider.bounds.min.z < rear.bounds.min.z) rear = collider;
        }
        var bounds = rear.bounds;
        p.WarpPlayer(new Vector3(bounds.center.x, 33.5f, bounds.min.z - 1.2f));
        await p.WaitSeconds(1f);
        // 画面下端のHUDにカーソルが重なるとインタラクト走査が空になるため、車両の上寄りを狙う
        // Aim high on the car because a cursor over the bottom HUD empties the interact scan
        await p.AimAt(new Vector3(bounds.center.x, bounds.max.y - 0.3f, bounds.min.z + 0.3f));
        p.Note($"aim screen point={Camera.main.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y - 0.3f, bounds.min.z + 0.3f))} screen={Screen.width}x{Screen.height}");
        await p.WaitSeconds(0.5f);
    }

    TrainCarEntityObject SpawnedCar() => UnityEngine.Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();

    #endregion
});
