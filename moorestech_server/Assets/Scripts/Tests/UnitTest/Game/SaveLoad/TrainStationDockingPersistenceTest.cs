using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Master;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainStationDockingPersistenceTest
    {
        [Test]
        public void DestroyingDockedTrainReleasesStationOccupancy()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var firstTrain = scenario.CreateForwardDockingTrain(out var firstCar);
            var secondTrain = scenario.CreateForwardDockingTrain(out var secondCar);

            firstTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(firstCar.IsDocked, "先頭列車が駅にドッキングできていません。");

            secondTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsFalse(secondCar.IsDocked, "駅占有中にもかかわらず後続列車がドッキングしています。");

            firstTrain.OnDestroy();

            secondTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(secondCar.IsDocked, "破棄後も後続列車が駅にドッキングできていません。");
        }

        [Test]
        public void ReloadingRestoresDockedTrainState()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var train = scenario.CreateForwardDockingTrain(out var car);
            train.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(car.IsDocked, "列車が駅にドッキングしていません。");

            var expectedItem = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 7);
            car.SetItem(0, expectedItem);

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(scenario.Environment.ServiceProvider);

            scenario.Dispose();

            var loadEnv = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            SaveLoadJsonTestHelper.LoadFromJson(loadEnv.ServiceProvider, saveJson);

            var loadedTrains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            Assert.AreEqual(1, loadedTrains.Count, "ロード後の登録列車数が一致しません。");

            var loadedTrain = loadedTrains[0];
            var loadedCar = loadedTrain.Cars[0];

            Assert.IsTrue(loadedTrain.trainUnitStationDocking.IsDocked, "ロード後に列車のドッキング状態が復元されていません。");
            Assert.IsTrue(loadedCar.IsDocked, "ロード後に車両のドッキング状態が復元されていません。");
            Assert.IsNotNull(loadedCar.dockingblock, "ロード後にドッキングブロックが割り当てられていません。");
            Assert.AreEqual(scenario.StationBlockPosition, loadedCar.dockingblock.BlockPositionInfo.OriginalPos,
                "ロード後のドッキング先ブロック位置が一致しません。");

            var loadedStack = loadedCar.GetItem(0);
            Assert.AreEqual(expectedItem.Id, loadedStack.Id, "ロード後の貨車インベントリIDが一致しません。");
            Assert.AreEqual(expectedItem.Count, loadedStack.Count, "ロード後の貨車インベントリ個数が一致しません。");

            CleanupTrains(loadedTrains);
        }

        [Test]
        public void LoadingCorruptedDockingDataUndocksTrainSafely()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var train = scenario.CreateForwardDockingTrain(out _);
            train.trainUnitStationDocking.TryDockWhenStopped();

            var loadEnv = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            SaveLoadJsonTestHelper.SaveCorruptAndLoad(
                scenario.Environment.ServiceProvider,
                loadEnv.ServiceProvider,
                json =>
                {
                    var root = JsonNode.Parse(json) ?? throw new InvalidOperationException("セーブJSONの解析に失敗しました。");
                    var trainUnits = root["trainUnits"] as JsonArray;
                    Assert.IsNotNull(trainUnits, "trainUnits 配列が存在しません。");

                    if (trainUnits!.Count > 0 && trainUnits[0] is JsonObject firstTrain)
                    {
                        if (firstTrain.TryGetPropertyValue("Cars", out var carsNode) && carsNode is JsonArray cars && cars.Count > 0)
                        {
                            if (cars[0] is JsonObject firstCar)
                            {
                                TrainCarJson.RemoveDockingBlockPosition(firstCar);
                            }
                        }
                    }

                    return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                });

            var loadedTrains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            Assert.AreEqual(1, loadedTrains.Count, "破損JSONロード後の登録列車数が一致しません。");

            var loadedTrain = loadedTrains[0];
            var loadedCar = loadedTrain.Cars[0];

            Assert.IsFalse(loadedTrain.trainUnitStationDocking.IsDocked, "破損JSONロード後にもドッキング状態が維持されています。");
            Assert.IsFalse(loadedCar.IsDocked, "破損JSONロード後にも車両がドッキング状態のままです。");
            Assert.IsNull(loadedCar.dockingblock, "破損JSONロード後にもドッキングブロック参照が残っています。");

            loadedTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(loadedCar.IsDocked, "破損JSONロード後に再ドッキングできません。");

            CleanupTrains(loadedTrains);
        }

        [Test]
        public void MultipleTrainsPreserveStateAcrossSaveLoad()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var dockedTrain = scenario.CreateForwardDockingTrain(out var dockedCar);
            var runningTrain = scenario.CreateOpposingDockingTrain(out var runningCar, scenario.StationSegmentLength / 2);

            var dockedSnapshot = ConfigureDockedTrain(dockedTrain, dockedCar, scenario);
            var runningSnapshot = ConfigureRunningTrain(runningTrain, runningCar, scenario);

            var registeredCount = TrainUpdateService.Instance.GetRegisteredTrains().Count();
            Assert.AreEqual(2, registeredCount, "セーブ前の登録列車数が想定と異なります。");

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(scenario.Environment.ServiceProvider);

            scenario.Dispose();

            var loadEnv = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            SaveLoadJsonTestHelper.LoadFromJson(loadEnv.ServiceProvider, saveJson);

            var loadedTrains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            Assert.AreEqual(2, loadedTrains.Count, "ロード後の登録列車数が一致しません。");

            var loadedDockedTrain = loadedTrains.Single(train => train.trainUnitStationDocking.IsDocked);
            var loadedRunningTrain = loadedTrains.Single(train => !train.trainUnitStationDocking.IsDocked);

            AssertTrainStateMatches(loadedDockedTrain, dockedSnapshot);
            AssertTrainStateMatches(loadedRunningTrain, runningSnapshot);

            CleanupTrains(loadedTrains);
        }

        private static TrainStateSnapshot ConfigureDockedTrain(TrainUnit train, TrainCar car, TrainStationDockingScenario scenario)
        {
            var stationExit = scenario.StationExitFront;
            var stationEntry = scenario.StationEntryFront;
            var backExit = scenario.StationExitBack;
            var backEntry = scenario.StationEntryBack;

            train.trainDiagram.AddEntry(stationExit);
            train.trainDiagram.AddEntry(stationEntry);
            train.trainDiagram.AddEntry(backExit);
            train.trainDiagram.AddEntry(backEntry);

            train.trainUnitStationDocking.TryDockWhenStopped();

            var waitEntry = train.trainDiagram.Entries[0];
            waitEntry.SetDepartureWaitTicks(5);

            var expectedItem = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 11);
            car.SetItem(0, expectedItem);

            train.TurnOnAutoRun();
            train.Update();
            train.Update();

            return TrainStateSnapshot.Create(train, car, waitEntry, scenario.StationBlockPosition);
        }

        private static TrainStateSnapshot ConfigureRunningTrain(TrainUnit train, TrainCar car, TrainStationDockingScenario scenario)
        {
            var exitBack = scenario.StationExitBack;
            var entryBack = scenario.StationEntryBack;
            var exitFront = scenario.StationExitFront;

            train.trainDiagram.AddEntry(exitBack);
            train.trainDiagram.AddEntry(entryBack);
            train.trainDiagram.AddEntry(exitFront);

            var expectedItem = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 3);
            car.SetItem(0, expectedItem);

            train.TurnOnAutoRun();
            for (var i = 0; i < 8; i++)
            {
                train.Update();
            }

            return TrainStateSnapshot.Create(train, car, null, null);
        }

        private static void AssertTrainStateMatches(TrainUnit train, TrainStateSnapshot expected)
        {
            Assert.AreEqual(expected.CarCount, train.Cars.Count, "車両数が一致しません。");
            Assert.AreEqual(expected.TrainLength, train.RailPosition.TrainLength, "列車長が一致しません。");
            Assert.AreEqual(expected.IsAutoRun, train.IsAutoRun, "自動運転状態が一致しません。");
            Assert.AreEqual(expected.DiagramEntryCount, train.trainDiagram.Entries.Count, "ダイアグラムのエントリ数が一致しません。");
            Assert.AreEqual(expected.DiagramCurrentIndex, train.trainDiagram.CurrentIndex, "ダイアグラムの現在インデックスが一致しません。");
            Assert.AreEqual(expected.DistanceToNextNode, train.RailPosition.GetDistanceToNextNode(), "次ノードまでの距離が一致しません。");
            Assert.AreEqual(expected.CurrentSpeed, train.CurrentSpeed, 1e-6, "速度が一致しません。");

            var currentIndex = Mathf.Clamp(train.trainDiagram.CurrentIndex, 0, train.trainDiagram.Entries.Count - 1);
            var activeEntry = train.trainDiagram.Entries[currentIndex];
            if (expected.WaitInitialTicks.HasValue)
            {
                Assert.AreEqual(expected.WaitInitialTicks, activeEntry.GetWaitForTicksInitialTicks(), "待機ティック初期値が一致しません。");
                Assert.AreEqual(expected.WaitRemainingTicks, activeEntry.GetWaitForTicksRemainingTicks(), "残り待機ティックが一致しません。");
            }
            else
            {
                Assert.IsNull(activeEntry.GetWaitForTicksInitialTicks(), "待機ティック初期値が不要にも設定されています。");
                Assert.IsNull(activeEntry.GetWaitForTicksRemainingTicks(), "待機ティック残数が不要にも設定されています。");
            }

            Assert.AreEqual(expected.FirstEntryNodeRole, activeEntry.Node.StationRef.NodeRole, "先頭エントリのノード役割が一致しません。");
            Assert.AreEqual(expected.FirstEntryNodeSide, activeEntry.Node.StationRef.NodeSide, "先頭エントリのノード方向が一致しません。");

            var loadedCar = train.Cars[0];
            var loadedStack = loadedCar.GetItem(0);
            Assert.AreEqual(expected.InventoryItemId, loadedStack.Id, "貨車インベントリIDが一致しません。");
            Assert.AreEqual(expected.InventoryCount, loadedStack.Count, "貨車インベントリ個数が一致しません。");

            Assert.AreEqual(expected.IsDocked, train.trainUnitStationDocking.IsDocked, "ドッキング状態が一致しません。");

            if (expected.DockingBlockPosition.HasValue)
            {
                Assert.IsNotNull(loadedCar.dockingblock, "ドッキングブロックが存在しません。");
                Assert.AreEqual(expected.DockingBlockPosition.Value, loadedCar.dockingblock.BlockPositionInfo.OriginalPos,
                    "ドッキングブロック位置が一致しません。");
            }
            else
            {
                Assert.IsNull(loadedCar.dockingblock, "ドッキングブロック参照が不要にも残っています。");
            }
        }

        private static void CleanupTrains(IEnumerable<TrainUnit> trains)
        {
            foreach (var train in trains)
            {
                train.trainUnitStationDocking.UndockFromStation();
                TrainDiagramManager.Instance.UnregisterDiagram(train.trainDiagram);
                TrainUpdateService.Instance.UnregisterTrain(train);
            }
        }

        private readonly struct TrainStateSnapshot
        {
            private TrainStateSnapshot(
                int carCount,
                int trainLength,
                bool isAutoRun,
                int diagramEntryCount,
                int diagramCurrentIndex,
                int distanceToNextNode,
                double currentSpeed,
                int? waitInitialTicks,
                int? waitRemainingTicks,
                StationNodeRole firstEntryNodeRole,
                StationNodeSide firstEntryNodeSide,
                ItemId inventoryItemId,
                int inventoryCount,
                bool isDocked,
                Vector3Int? dockingBlockPosition)
            {
                CarCount = carCount;
                TrainLength = trainLength;
                IsAutoRun = isAutoRun;
                DiagramEntryCount = diagramEntryCount;
                DiagramCurrentIndex = diagramCurrentIndex;
                DistanceToNextNode = distanceToNextNode;
                CurrentSpeed = currentSpeed;
                WaitInitialTicks = waitInitialTicks;
                WaitRemainingTicks = waitRemainingTicks;
                FirstEntryNodeRole = firstEntryNodeRole;
                FirstEntryNodeSide = firstEntryNodeSide;
                InventoryItemId = inventoryItemId;
                InventoryCount = inventoryCount;
                IsDocked = isDocked;
                DockingBlockPosition = dockingBlockPosition;
            }

            public int CarCount { get; }
            public int TrainLength { get; }
            public bool IsAutoRun { get; }
            public int DiagramEntryCount { get; }
            public int DiagramCurrentIndex { get; }
            public int DistanceToNextNode { get; }
            public double CurrentSpeed { get; }
            public int? WaitInitialTicks { get; }
            public int? WaitRemainingTicks { get; }
            public StationNodeRole FirstEntryNodeRole { get; }
            public StationNodeSide FirstEntryNodeSide { get; }
            public ItemId InventoryItemId { get; }
            public int InventoryCount { get; }
            public bool IsDocked { get; }
            public Vector3Int? DockingBlockPosition { get; }

            public static TrainStateSnapshot Create(TrainUnit train, TrainCar car, TrainDiagram.DiagramEntry? waitEntry, Vector3Int? dockingBlockPosition)
            {
                var currentIndex = Mathf.Clamp(train.trainDiagram.CurrentIndex, 0, train.trainDiagram.Entries.Count - 1);
                var activeEntry = train.trainDiagram.Entries[currentIndex];
                int? waitInitial = waitEntry?.GetWaitForTicksInitialTicks();
                int? waitRemaining = waitEntry?.GetWaitForTicksRemainingTicks();

                var stack = car.GetItem(0);

                return new TrainStateSnapshot(
                    train.Cars.Count,
                    train.RailPosition.TrainLength,
                    train.IsAutoRun,
                    train.trainDiagram.Entries.Count,
                    train.trainDiagram.CurrentIndex,
                    train.RailPosition.GetDistanceToNextNode(),
                    train.CurrentSpeed,
                    waitInitial,
                    waitRemaining,
                    activeEntry.Node.StationRef.NodeRole,
                    activeEntry.Node.StationRef.NodeSide,
                    stack.Id,
                    stack.Count,
                    train.trainUnitStationDocking.IsDocked,
                    dockingBlockPosition);
            }
        }
    }
}
