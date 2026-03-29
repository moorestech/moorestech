using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainStationDockingFluidTransferTest
    {
        private static readonly Guid WaterGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void FluidPlatformTransfersFluidToDockedTrainCar()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(platformBlock);
            Assert.IsNotNull(railComponents);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponent = platformBlock.GetComponent<TrainPlatformDockingComponent>();

            Assert.IsNotNull(fluidContainerComponent);
            Assert.IsNotNull(dockingComponent);

            // プラットフォームに液体を注入
            // Inject fluid into the platform
            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var addAmount = 500.0;
            fluidContainerComponent.AddLiquid(new FluidStack(addAmount, waterFluidId), FluidContainer.Empty);

            Assert.IsNotNull(fluidContainerComponent.Container);
            Assert.AreEqual(addAmount, fluidContainerComponent.Container.Container.Amount, 0.001);

            // 列車カーを生成してドッキング
            // Create train car and dock
            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            var (trainCar, fluidContainer) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 1000, 1000, segmentLength, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked);
            Assert.IsTrue(trainCar.IsInventoryEmpty());

            // アーム伸長 + 転送完了まで Update
            // Update until arm extends and transfer completes
            var transferTicks = GetFluidPlatformTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                dockingComponent.Update();
                fluidContainerComponent.Update();
            }

            // プラットフォームの液体が列車に移った
            // Platform fluid transferred to train
            Assert.AreEqual(addAmount, fluidContainer.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, fluidContainer.Container.FluidId);

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void FluidPlatformReceivesFluidFromTrainCarWhenInUnloadMode()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(platformBlock);
            Assert.IsNotNull(railComponents);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponent = platformBlock.GetComponent<TrainPlatformDockingComponent>();
            var transferComponent = platformBlock.GetComponent<TrainPlatformTransferComponent>();

            Assert.IsNotNull(fluidContainerComponent);
            Assert.IsNotNull(dockingComponent);
            Assert.IsNotNull(transferComponent);

            // UnloadモードにSet
            // Set to unload mode
            transferComponent.SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            // 液体入りの列車カーを生成
            // Create train car with fluid
            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var fluidAmount = 500.0;

            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            var (trainCar, fluidContainer) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 1000, 1000, segmentLength, true);
            fluidContainer.Container.FluidId = waterFluidId;
            fluidContainer.Container.Amount = fluidAmount;

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked);

            // アーム伸長 + 転送完了まで Update
            // Update until arm extends and transfer completes
            var transferTicks = GetFluidPlatformTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                dockingComponent.Update();
                fluidContainerComponent.Update();
            }

            // 列車の液体がプラットフォームに移った
            // Train fluid transferred to platform
            Assert.IsNotNull(fluidContainerComponent.Container);
            Assert.AreEqual(fluidAmount, fluidContainerComponent.Container.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, fluidContainerComponent.Container.Container.FluidId);
            Assert.AreSame(fluidContainer, fluidContainerComponent.Container);

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void FluidPlatformTransfersToTrainWithExistingFluid()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponent = platformBlock.GetComponent<TrainPlatformDockingComponent>();

            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);

            // プラットフォームに液体を注入
            // Inject fluid into the platform
            var platformAmount = 300.0;
            fluidContainerComponent.AddLiquid(new FluidStack(platformAmount, waterFluidId), FluidContainer.Empty);

            // 列車カーを生成（既に液体が入っている）
            // Create train car already containing fluid
            var trainExistingAmount = 200.0;
            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            var (trainCar, fluidContainer) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 1000, 1000, segmentLength, true);
            fluidContainer.Container.FluidId = waterFluidId;
            fluidContainer.Container.Amount = trainExistingAmount;

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked);

            // アーム伸長 + 転送完了まで Update
            // Update until arm extends and transfer completes
            var transferTicks = GetFluidPlatformTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                dockingComponent.Update();
                fluidContainerComponent.Update();
            }

            // 合計量が列車に移った
            // Total amount transferred to train
            var expectedTotal = platformAmount + trainExistingAmount;
            Assert.AreEqual(expectedTotal, fluidContainer.Container.Amount, 0.001);

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void FluidPlatformDoesNotMixDifferentFluids()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponent = platformBlock.GetComponent<TrainPlatformDockingComponent>();

            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var steamGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
            var steamFluidId = MasterHolder.FluidMaster.GetFluidId(steamGuid);

            // プラットフォームに水を注入
            // Inject water into the platform
            var platformAmount = 300.0;
            fluidContainerComponent.AddLiquid(new FluidStack(platformAmount, waterFluidId), FluidContainer.Empty);

            // 列車カーにSteamが入っている
            // Train car contains steam
            var trainSteamAmount = 200.0;
            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            var (trainCar, fluidContainer) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 1000, 1000, segmentLength, true);
            fluidContainer.Container.FluidId = steamFluidId;
            fluidContainer.Container.Amount = trainSteamAmount;

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked);

            // アーム伸長 + 転送完了まで Update
            // Update until arm extends and transfer completes
            var transferTicks = GetFluidPlatformTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                dockingComponent.Update();
                fluidContainerComponent.Update();
            }

            // 異液体なので転送されない
            // Different fluids should not mix
            Assert.AreEqual(platformAmount, fluidContainerComponent.Container.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, fluidContainerComponent.Container.Container.FluidId);
            Assert.AreEqual(trainSteamAmount, fluidContainer.Container.Amount, 0.001);
            Assert.AreEqual(steamFluidId, fluidContainer.Container.FluidId);

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void FluidPlatformMovesContainerWhenTrainCarIsEmpty()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponent = platformBlock.GetComponent<TrainPlatformDockingComponent>();

            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var platformAmount = 500.0;
            fluidContainerComponent.AddLiquid(new FluidStack(platformAmount, waterFluidId), FluidContainer.Empty);

            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            // コンテナなしの列車カーを生成
            // Create train car without container
            var element = TrainTestCarFactory.CreateMasterElement(0, 1000, 0, segmentLength);
            var trainCar = new TrainCar(element, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked);
            Assert.IsNull(trainCar.Container);

            var transferTicks = GetFluidPlatformTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                dockingComponent.Update();
                fluidContainerComponent.Update();
            }

            // コンテナごと列車に移動した
            // Entire container moved to train
            Assert.IsNull(fluidContainerComponent.Container);
            Assert.IsNotNull(trainCar.Container);
            var trainFluidContainer = trainCar.Container as FluidTrainCarContainer;
            Assert.IsNotNull(trainFluidContainer);
            Assert.AreEqual(platformAmount, trainFluidContainer.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, trainFluidContainer.Container.FluidId);

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void FluidPlatformLoadPartialWhenTrainCapacityExceeded()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponent = platformBlock.GetComponent<TrainPlatformDockingComponent>();

            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);

            // プラットフォームに液体を注入
            // Inject fluid into the platform
            var platformAmount = 700.0;
            fluidContainerComponent.AddLiquid(new FluidStack(platformAmount, waterFluidId), FluidContainer.Empty);

            // 容量制限のある列車カーを生成（既に液体が入っている）
            // Create train car with limited remaining capacity
            var trainCapacity = 500.0;
            var trainExistingAmount = 300.0;
            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            var (trainCar, fluidContainer) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 1000, trainCapacity, segmentLength, true);
            fluidContainer.Container.FluidId = waterFluidId;
            fluidContainer.Container.Amount = trainExistingAmount;

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked);

            // アーム伸長 + 転送完了まで Update
            // Update until arm extends and transfer completes
            var transferTicks = GetFluidPlatformTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                dockingComponent.Update();
                fluidContainerComponent.Update();
            }

            // 列車が満杯、残りはプラットフォームに留まる
            // Train is full, remainder stays on platform
            var trainRemainingCapacity = trainCapacity - trainExistingAmount;
            Assert.AreEqual(trainCapacity, fluidContainer.Container.Amount, 0.001);
            Assert.AreEqual(platformAmount - trainRemainingCapacity, fluidContainerComponent.Container.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, fluidContainer.Container.FluidId);
            Assert.AreEqual(waterFluidId, fluidContainerComponent.Container.Container.FluidId);

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void FluidPlatformUnloadPartialWhenPlatformCapacityExceeded()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            var dockingComponent = platformBlock.GetComponent<TrainPlatformDockingComponent>();
            var transferComponent = platformBlock.GetComponent<TrainPlatformTransferComponent>();

            // Unloadモードに設定
            // Set to unload mode
            transferComponent.SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);

            // プラットフォームに既存の液体を注入（容量1000のうち800）
            // Inject existing fluid into platform (800 of 1000 capacity)
            var platformExistingAmount = 800.0;
            fluidContainerComponent.AddLiquid(new FluidStack(platformExistingAmount, waterFluidId), FluidContainer.Empty);

            // 列車カーに液体を設定
            // Set fluid on train car
            var trainAmount = 500.0;
            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            var (trainCar, fluidContainer) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 1000, 1000, segmentLength, true);
            fluidContainer.Container.FluidId = waterFluidId;
            fluidContainer.Container.Amount = trainAmount;

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked);

            // アーム伸長 + 転送完了まで Update
            // Update until arm extends and transfer completes
            var transferTicks = GetFluidPlatformTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                dockingComponent.Update();
                fluidContainerComponent.Update();
            }

            // プラットフォームが満杯、残りは列車に留まる
            // Platform is full, remainder stays on train
            var platformRemainingCapacity = 1000.0 - platformExistingAmount;
            Assert.AreEqual(1000.0, fluidContainerComponent.Container.Container.Amount, 0.001);
            Assert.AreEqual(trainAmount - platformRemainingCapacity, fluidContainer.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, fluidContainerComponent.Container.Container.FluidId);
            Assert.AreEqual(waterFluidId, fluidContainer.Container.FluidId);

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        #region Internal

        private static int GetFluidPlatformTransferTicks()
        {
            var param = (TrainFluidPlatformBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainFluidPlatform).BlockParam;
            var ticks = GameUpdater.SecondsToTicks(param.LoadingAnimeSpeed);
            return (ticks > int.MaxValue ? int.MaxValue : (int)ticks) + 1;
        }

        #endregion
    }
}
