using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainBidirectionalFluidLoopSaveDataTest
    {
        private static readonly Guid WaterGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        /// <summary>
        /// 2つの液体貨物駅と互いに逆方向へ走る2編成の列車を構築し、液体の積み下ろし循環ができるか試すテスト
        /// 駅A,Bに水を満載し、train1がAで積載→Bで荷降ろし、train2がBで積載→Aで荷降ろしを確認する
        /// </summary>
        [Test]
        public void BidirectionalFluidLoopScenarioProducesExpectedSaveData()
        {
            var environment = TrainTestHelper.CreateEnvironment();

            // 駅A,Bを設置
            // Place stations A and B
            var (stationABlock, stationAComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                environment,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                new Vector3Int(1, 0, 0),
                BlockDirection.North);

            var (stationBBlock, stationBComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                environment,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                new Vector3Int(0, 0, 24),
                BlockDirection.North);

            Assert.IsNotNull(stationABlock);
            Assert.IsNotNull(stationAComponents);
            Assert.IsNotNull(stationBBlock);
            Assert.IsNotNull(stationBComponents);

            var stationA = ExtractStationEndpoints(stationAComponents);
            var stationB = ExtractStationEndpoints(stationBComponents);

            // レールを接続（A→BとB→Aのループ）
            // Connect rails (A→B and B→A loop)
            const int TransitSegmentLength = 5000;
            stationA.ExitComponent.FrontNode.ConnectNode(stationB.EntryComponent.FrontNode, TransitSegmentLength);
            stationB.EntryComponent.BackNode.ConnectNode(stationA.ExitComponent.BackNode, TransitSegmentLength);
            stationB.ExitComponent.FrontNode.ConnectNode(stationA.EntryComponent.FrontNode, TransitSegmentLength);
            stationA.EntryComponent.BackNode.ConnectNode(stationB.ExitComponent.BackNode, TransitSegmentLength);

            var stationSegmentLength = stationABlock.BlockPositionInfo.BlockSize.z;

            // 各駅のコンポーネントを取得
            // Get components for each station
            var fluidComponentA = stationABlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponentA = stationABlock.GetComponent<TrainPlatformDockingComponent>();
            var transferComponentA = stationABlock.GetComponent<TrainPlatformTransferComponent>();
            var fluidComponentB = stationBBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponentB = stationBBlock.GetComponent<TrainPlatformDockingComponent>();
            var transferComponentB = stationBBlock.GetComponent<TrainPlatformTransferComponent>();

            Assert.IsNotNull(fluidComponentA);
            Assert.IsNotNull(dockingComponentA);
            Assert.IsNotNull(transferComponentA);
            Assert.IsNotNull(fluidComponentB);
            Assert.IsNotNull(dockingComponentB);
            Assert.IsNotNull(transferComponentB);

            // 駅A,Bに水を満載（容量1000）
            // Fill stations A and B with water (capacity 1000)
            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            fluidComponentA.AddLiquid(new FluidStack(1000.0, waterFluidId), FluidContainer.Empty);
            fluidComponentB.AddLiquid(new FluidStack(1000.0, waterFluidId), FluidContainer.Empty);

            // 列車を2編成生成（液体コンテナ付き）
            // Create two trains with fluid containers
            var (train1Car, _) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 20000, 1000, stationSegmentLength, true);
            var train1Nodes = new List<IRailNode> { stationA.ExitFront, stationA.EntryFront };
            var train1 = new TrainUnit(
                new RailPosition(train1Nodes, train1Car.Length, 0),
                new List<TrainCar> { train1Car },
                environment.GetTrainUpdateService(),
                environment.GetTrainRailPositionManager(),
                environment.GetTrainDiagramManager());

            var (train2Car, _) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 20000, 1000, stationSegmentLength, false);
            var train2Nodes = new List<IRailNode> { stationB.ExitFront, stationB.EntryFront };
            var train2 = new TrainUnit(
                new RailPosition(train2Nodes, train2Car.Length, 0),
                new List<TrainCar> { train2Car },
                environment.GetTrainUpdateService(),
                environment.GetTrainRailPositionManager(),
                environment.GetTrainDiagramManager());
            train2.Reverse();

            // ダイアグラムを設定
            // Configure diagrams
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

            for (var tick = 0; tick < 20000; tick++)
            {
                UpdateTransferModes();
                train1.Update();
                train2.Update();

                // 貨物アームのtickを進める
                // Advance cargo platform arm ticks
                dockingComponentA.Update();
                fluidComponentA.Update();
                dockingComponentB.Update();
                fluidComponentB.Update();

                if (!train1LoadedAtA && train1Car.IsInventoryFull() && ReferenceEquals(train1Car.dockingblock, stationABlock))
                    train1LoadedAtA = true;

                if (!train1UnloadedAtB && train1Car.IsInventoryEmpty() && ReferenceEquals(train1Car.dockingblock, stationBBlock))
                    train1UnloadedAtB = true;

                if (!train2LoadedAtB && train2Car.IsInventoryFull() && ReferenceEquals(train2Car.dockingblock, stationBBlock))
                    train2LoadedAtB = true;

                if (!train2UnloadedAtA && train2Car.IsInventoryEmpty() && ReferenceEquals(train2Car.dockingblock, stationABlock))
                    train2UnloadedAtA = true;

                if (train1LoadedAtA && train1UnloadedAtB && train2LoadedAtB && train2UnloadedAtA)
                    break;
            }

            Assert.IsTrue(train1LoadedAtA, "Train1 が Station A で液体の積載を完了していません。");
            Assert.IsTrue(train1UnloadedAtB, "Train1 が Station B で液体の荷降ろしを完了していません。");
            Assert.IsTrue(train2LoadedAtB, "Train2 が Station B で液体の積載を完了していません。");
            Assert.IsTrue(train2UnloadedAtA, "Train2 が Station A で液体の荷降ろしを完了していません。");

            // セーブデータを検証
            // Verify save data
            var train1Save = train1.CreateSaveData();
            var train2Save = train2.CreateSaveData();

            Assert.AreEqual(1, train1Save.Cars.Count);
            Assert.AreEqual(1, train2Save.Cars.Count);

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            Assert.IsTrue(saveJson.Contains("trainUnits"));

            environment.GetTrainUpdateService().ResetTrains();

            #region Internal

            void UpdateTransferModes()
            {
                // train1: 駅Aで積載、駅Bで荷降ろし
                // train1: load at station A, unload at station B
                if (train1Car.IsDocked)
                {
                    if (ReferenceEquals(train1Car.dockingblock, stationABlock))
                        transferComponentA.SetMode(TrainPlatformTransferComponent.TransferMode.LoadToTrain);
                    else if (ReferenceEquals(train1Car.dockingblock, stationBBlock))
                        transferComponentB.SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);
                }

                // train2: 駅Bで積載、駅Aで荷降ろし
                // train2: load at station B, unload at station A
                if (train2Car.IsDocked)
                {
                    if (ReferenceEquals(train2Car.dockingblock, stationBBlock))
                        transferComponentB.SetMode(TrainPlatformTransferComponent.TransferMode.LoadToTrain);
                    else if (ReferenceEquals(train2Car.dockingblock, stationABlock))
                        transferComponentA.SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);
                }
            }

            #endregion
        }

        private static StationEndpoints ExtractStationEndpoints(IReadOnlyList<RailComponent> components)
        {
            var entryComponent = components.First(c => c.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
            var exitComponent = components.First(c => c.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);

            return new StationEndpoints(
                entryComponent, exitComponent,
                entryComponent.FrontNode, exitComponent.FrontNode,
                exitComponent.BackNode, entryComponent.BackNode);
        }

        private readonly struct StationEndpoints
        {
            public StationEndpoints(
                RailComponent entryComponent, RailComponent exitComponent,
                RailNode entryFront, RailNode exitFront,
                RailNode entryBack, RailNode exitBack)
            {
                EntryComponent = entryComponent;
                ExitComponent = exitComponent;
                EntryFront = entryFront;
                ExitFront = exitFront;
                EntryBack = entryBack;
                ExitBack = exitBack;
            }

            public RailComponent EntryComponent { get; }
            public RailComponent ExitComponent { get; }
            public RailNode EntryFront { get; }
            public RailNode ExitFront { get; }
            public RailNode EntryBack { get; }
            public RailNode ExitBack { get; }
        }
    }
}
