using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainBidirectionalCargoLoopSaveDataTest
    {
        /// <summary>
        /// 2つの貨物駅と互いに逆方向へ走る2編成の列車を構築して自動運転させアイテム循環ができるか試すテスト
        /// 
        /// １、cargo駅1つを場所A,Bに1つずつ設置
        /// ２、cargo駅Aのfront exitとBのfront entryをつなぐ、cargo駅Bのfront exitとAのfront entryをつなぐ
        /// ３、trainunitを2つ生成。trainunit1はcarが1つ、前向き。trainunit2はcarが1つ後ろ向き。設置場所はそれぞれ駅AとBに。carのlengthは駅と同じ。
        /// trainunitの先頭いちはどちらも駅のexitにあればいい。設置に関してはtrainunit1、trainunit2とも駅ABのfrontのentryからexit向きにしておく。次にtrainunit2をreverseする。これでお互い反対方向に向かうことになる
        /// ４、ダイアグラムを設定
        /// trainunit1：駅Aで貨物満載で発車、Bで貨物空で発車
        /// trainunit2：駅Aで貨物空で発車、Bで貨物満載で発車
        /// ５，update。各駅でドッキングしているかを確認し、ドッキングしていれば駅のcargoプラットフォームのモードをプログラムで切り替える(ここは実プレイと乖離があるところ、実際は駅のload unloadの設定をしたらあとは放置)
        /// </summary>
        [Test]
        public void BidirectionalCargoLoopScenarioProducesExpectedSaveData()
        {
            _ = new TrainDiagramManager();
            TrainUpdateService.Instance.ResetTrains();

            var environment = TrainTestHelper.CreateEnvironment();

            var (stationABlock, stationASaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(1, 0, 0),
                BlockDirection.North);

            var (stationBBlock, stationBSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(0, 0, 24),
                BlockDirection.North);

            Assert.IsNotNull(stationABlock, "Station A のブロック生成に失敗しました。");
            Assert.IsNotNull(stationASaver, "Station A の RailSaverComponent 取得に失敗しました。");
            Assert.IsNotNull(stationBBlock, "Station B のブロック生成に失敗しました。");
            Assert.IsNotNull(stationBSaver, "Station B の RailSaverComponent 取得に失敗しました。");

            var stationA = ExtractStationEndpoints(stationASaver!);
            var stationB = ExtractStationEndpoints(stationBSaver!);

            const int TransitSegmentLength = 5000;
            stationA.ExitComponent.ConnectRailComponent(stationB.EntryComponent, true, true, TransitSegmentLength);
            stationB.ExitComponent.ConnectRailComponent(stationA.EntryComponent, true, true, TransitSegmentLength);

            var stationSegmentLength = stationA.EntryFront.GetDistanceToNode(stationA.ExitFront);

            Assert.IsTrue(stationABlock!.ComponentManager.TryGetComponent<IBlockInventory>(out var inventoryA),
                "Station A のインベントリ取得に失敗しました。");
            Assert.IsTrue(stationBBlock!.ComponentManager.TryGetComponent<IBlockInventory>(out var inventoryB),
                "Station B のインベントリ取得に失敗しました。");

            var cargoA = stationABlock.GetComponent<CargoplatformComponent>();
            var cargoB = stationBBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoA, "Station A の CargoplatformComponent が取得できません。");
            Assert.IsNotNull(cargoB, "Station B の CargoplatformComponent が取得できません。");

            var item1 = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1);
            var maxStack1 = item1.MaxStack;
            var item2 = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId2);
            var maxStack2 = item2.MaxStack;

            inventoryA!.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack1));
            inventoryA.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack1));
            inventoryB!.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, maxStack2));
            inventoryB.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, maxStack2));

            var train1Nodes = new List<IRailNode>
            {
                stationA.ExitFront,
                stationA.EntryFront
            };
            var train1Car = new TrainCar(new TrainCarMasterElement(0, Guid.Empty, Guid.Empty, null, 20000, 2, stationSegmentLength));
            var train1 = new TrainUnit(new RailPosition(train1Nodes, train1Car.Length, 0), new List<TrainCar> { train1Car });

            var train2Nodes = new List<IRailNode>
            {
                stationB.ExitFront,
                stationB.EntryFront
            };
            var train2Car = new TrainCar(new TrainCarMasterElement(0, Guid.Empty, Guid.Empty, null, 1000, 2, stationSegmentLength), isFacingForward: false);
            var train2 = new TrainUnit(new RailPosition(train2Nodes, train2Car.Length, 0), new List<TrainCar> { train2Car });
            train2.Reverse();
            //train1.trainUnitStationDocking.TryDockWhenStopped();
            //train2.trainUnitStationDocking.TryDockWhenStopped();

            var aDeparture = train1.trainDiagram.AddEntry(stationA.ExitFront);
            aDeparture.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);
            var bArrival = train1.trainDiagram.AddEntry(stationB.ExitFront);
            bArrival.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            var bDeparture = train2.trainDiagram.AddEntry(stationB.ExitBack);
            bDeparture.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);
            var aArrival = train2.trainDiagram.AddEntry(stationA.ExitBack);
            aArrival.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            train1.TurnOnAutoRun();
            train2.TurnOnAutoRun();

            var train1LoadedAtA = false;
            var train1UnloadedAtB = false;
            var train2LoadedAtB = false;
            var train2UnloadedAtA = false;

            void UpdateTransferModes()
            {
                if (train1Car.IsDocked)
                {
                    if (ReferenceEquals(train1Car.dockingblock, stationABlock))
                    {
                        cargoA.SetTransferMode(CargoplatformComponent.CargoTransferMode.LoadToTrain);
                    }
                    else if (ReferenceEquals(train1Car.dockingblock, stationBBlock))
                    {
                        cargoB.SetTransferMode(CargoplatformComponent.CargoTransferMode.UnloadToPlatform);
                    }
                }

                if (train2Car.IsDocked)
                {
                    if (ReferenceEquals(train2Car.dockingblock, stationBBlock))
                    {
                        cargoB.SetTransferMode(CargoplatformComponent.CargoTransferMode.LoadToTrain);
                    }
                    else if (ReferenceEquals(train2Car.dockingblock, stationABlock))
                    {
                        cargoA.SetTransferMode(CargoplatformComponent.CargoTransferMode.UnloadToPlatform);
                    }
                }
            }

            for (var tick = 0; tick < 20000; tick++)
            {
                UpdateTransferModes();
                train1.Update();
                train2.Update();

                if (!train1LoadedAtA && train1Car.IsInventoryFull() && ReferenceEquals(train1Car.dockingblock, stationABlock))
                {
                    train1LoadedAtA = true;
                }

                if (!train1UnloadedAtB && train1Car.IsInventoryEmpty() && ReferenceEquals(train1Car.dockingblock, stationBBlock))
                {
                    train1UnloadedAtB = true;
                }

                if (!train2LoadedAtB && train2Car.IsInventoryFull() && ReferenceEquals(train2Car.dockingblock, stationBBlock))
                {
                    train2LoadedAtB = true;
                }

                if (!train2UnloadedAtA && train2Car.IsInventoryEmpty() && ReferenceEquals(train2Car.dockingblock, stationABlock))
                {
                    train2UnloadedAtA = true;
                }

                if (train1LoadedAtA && train1UnloadedAtB && train2LoadedAtB && train2UnloadedAtA)
                {
                    break;
                }
            }

            Assert.IsTrue(train1LoadedAtA, "Train1 が Station A で積み込みを完了していません。");
            Assert.IsTrue(train1UnloadedAtB, "Train1 が Station B で荷降ろしを完了していません。");
            Assert.IsTrue(train2LoadedAtB, "Train2 が Station B で積み込みを完了していません。");
            Assert.IsTrue(train2UnloadedAtA, "Train2 が Station A で荷降ろしを完了していません。");

            var train1Save = train1.CreateSaveData();
            var train2Save = train2.CreateSaveData();

            Assert.AreEqual(1, train1Save.Cars.Count, "Train1 の車両数が一致しません。");
            Assert.AreEqual(1, train2Save.Cars.Count, "Train2 の車両数が一致しません。");

            var train1CarSave = train1Save.Cars[0];
            var train2CarSave = train2Save.Cars[0];

            Assert.AreEqual(true, train1CarSave.IsFacingForward, "Train1 の車両向きが保存されていません。");
            Assert.AreEqual(true, train2CarSave.IsFacingForward, "Train2 の車両向きが反映されていません。");
            /*
            Assert.That(train1Save.Diagram.Entries.SelectMany(entry => entry.DepartureConditions),
                Does.Contain(TrainDiagram.DepartureConditionType.TrainInventoryFull)
                    .And.Contain(TrainDiagram.DepartureConditionType.TrainInventoryEmpty),
                "Train1 のダイアグラム保存内容が想定と一致しません。");

            Assert.That(train2Save.Diagram.Entries.SelectMany(entry => entry.DepartureConditions),
                Does.Contain(TrainDiagram.DepartureConditionType.TrainInventoryFull)
                    .And.Contain(TrainDiagram.DepartureConditionType.TrainInventoryEmpty),
                "Train2 のダイアグラム保存内容が想定と一致しません。");
            */
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            Assert.IsTrue(saveJson.Contains("trainUnits"), "セーブデータに trainUnits セクションが含まれていません。");
            Debug.Log(saveJson);
            TrainUpdateService.Instance.ResetTrains();
        }

        private static StationEndpoints ExtractStationEndpoints(RailSaverComponent saver)
        {
            var entryComponent = saver.RailComponents
                .First(component => component.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
            var exitComponent = saver.RailComponents
                .First(component => component.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);

            var entryFront = entryComponent.FrontNode;
            var exitFront = exitComponent.FrontNode;
            var entryBack = exitComponent.BackNode;
            var exitBack = entryComponent.BackNode;
            var segmentLength = entryFront.GetDistanceToNode(exitFront);

            Assert.Greater(segmentLength, 0, "駅セグメント長が0以下です。");

            return new StationEndpoints(entryComponent, exitComponent, entryFront, exitFront, entryBack, exitBack, segmentLength);
        }

        private readonly struct StationEndpoints
        {
            public StationEndpoints(
                RailComponent entryComponent,
                RailComponent exitComponent,
                RailNode entryFront,
                RailNode exitFront,
                RailNode entryBack,
                RailNode exitBack,
                int segmentLength)
            {
                EntryComponent = entryComponent;
                ExitComponent = exitComponent;
                EntryFront = entryFront;
                ExitFront = exitFront;
                EntryBack = entryBack;
                ExitBack = exitBack;
                SegmentLength = segmentLength;
            }

            public RailComponent EntryComponent { get; }
            public RailComponent ExitComponent { get; }
            public RailNode EntryFront { get; }
            public RailNode ExitFront { get; }
            public RailNode EntryBack { get; }
            public RailNode ExitBack { get; }
            public int SegmentLength { get; }
        }
    }
}
