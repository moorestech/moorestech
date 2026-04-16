using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Component;
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

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainFluidPlatformSaveLoadTest
    {
        private static readonly Guid WaterGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void FluidPlatformTransferModesPersistAcrossSaveLoad()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var loadPosition = new Vector3Int(12, 0, 0);
            var unloadPosition = new Vector3Int(28, 0, 0);

            var loadBlock = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainFluidPlatform, loadPosition, BlockDirection.North);
            var unloadBlock = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainFluidPlatform, unloadPosition, BlockDirection.North);

            Assert.IsNotNull(loadBlock);
            Assert.IsNotNull(unloadBlock);

            var loadTransfer = loadBlock.GetComponent<TrainPlatformTransferComponent>();
            var unloadTransfer = unloadBlock.GetComponent<TrainPlatformTransferComponent>();

            Assert.IsNotNull(loadTransfer);
            Assert.IsNotNull(unloadTransfer);

            // 転送モードを設定
            // Set transfer modes
            loadTransfer.SetMode(TrainPlatformTransferComponent.TransferMode.LoadToTrain);
            unloadTransfer.SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            // セーブ & ロード
            // Save & Load
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            // ロード後のモードを検証
            // Verify modes after load
            var loadedLoadBlock = loadEnvironment.WorldBlockDatastore.GetBlock(loadPosition);
            var loadedUnloadBlock = loadEnvironment.WorldBlockDatastore.GetBlock(unloadPosition);

            Assert.IsNotNull(loadedLoadBlock);
            Assert.IsNotNull(loadedUnloadBlock);

            var loadedLoadTransfer = loadedLoadBlock.GetComponent<TrainPlatformTransferComponent>();
            var loadedUnloadTransfer = loadedUnloadBlock.GetComponent<TrainPlatformTransferComponent>();

            Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.LoadToTrain, loadedLoadTransfer.Mode);
            Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform, loadedUnloadTransfer.Mode);
        }

        [Test]
        public void FluidPlatformAnimationProgressPersistsAcrossSaveLoad()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var (platformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                environment,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(platformBlock);
            Assert.IsNotNull(railComponents);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            Assert.IsNotNull(fluidContainerComponent);

            // プラットフォームに液体を注入
            // Inject fluid into the platform
            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var addAmount = 500.0;
            fluidContainerComponent.AddLiquid(new FluidStack(addAmount, waterFluidId), FluidContainer.Empty);

            // マスタデータの列車カーを生成してドッキング（コンテナなし）
            // Create train car from master data and dock (no container)
            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = platformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, segmentLength, 0);

            var firstTrainCarMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First();
            var trainCar = new TrainCar(firstTrainCarMaster, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            environment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);
            
            trainUnit.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(trainCar.IsDocked);

            // アーム伸長の途中まで進める
            // Advance arm extension halfway
            var param = (TrainFluidPlatformBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainFluidPlatform).BlockParam;
            var totalTicks = GetArmAnimationTicks(param.LoadingAnimeSpeed);
            var elapsedTicks = totalTicks / 2;

            List<IUpdatableBlockComponent> updatableComponents = platformBlock.GetComponents<IUpdatableBlockComponent>();
            for (var i = 0; i < elapsedTicks; i++)
                foreach (var c in updatableComponents)
                    c.Update();

            // まだ転送されていないことを確認
            // Verify no transfer yet
            Assert.IsNull(trainCar.Container);

            // セーブ & ロード
            // Save & Load
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            // ロード後のブロックと列車を取得
            // Get loaded block and train
            var loadedBlock = loadEnvironment.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsNotNull(loadedBlock);
            
            var loadedTrain = loadEnvironment.GetITrainLookupDatastore().GetRegisteredTrains().Single();
            var loadedCar = loadedTrain.Cars[0];
            Assert.IsTrue(loadedCar.IsDocked);

            // 残りのtickを進めて転送を完了させる
            // Advance remaining ticks to complete transfer
            List<IUpdatableBlockComponent> loadedUpdatableComponents = loadedBlock.GetComponents<IUpdatableBlockComponent>();
            var remainingTicks = totalTicks + 1 - elapsedTicks;
            for (var i = 0; i < remainingTicks; i++)
                foreach (var c in loadedUpdatableComponents)
                    c.Update();

            // コンテナごと列車に移動したことを確認
            // Verify container moved to train
            var loadedFluidComponent = loadedBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            Assert.IsNull(loadedFluidComponent.Container);

            var trainFluidContainer = loadedCar.Container as FluidTrainCarContainer;
            Assert.IsNotNull(trainFluidContainer);
            Assert.AreEqual(addAmount, trainFluidContainer.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, trainFluidContainer.Container.FluidId);

            loadedTrain.trainUnitStationDocking.UndockFromStation();
            loadEnvironment.GetTrainDiagramManager().UnregisterDiagram(loadedTrain.trainDiagram);
            loadEnvironment.GetITrainUnitMutationDatastore().UnregisterTrain(loadedTrain);
        }

        [Test]
        public void FluidPlatformContainerPersistsAcrossSaveLoad()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var (platformBlock, _) = TrainTestHelper.PlaceBlockWithRailComponents(
                environment,
                ForUnitTestModBlockId.TestTrainFluidPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(platformBlock);

            var fluidContainerComponent = platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            Assert.IsNotNull(fluidContainerComponent);

            // プラットフォームに液体を注入
            // Inject fluid into the platform
            var waterFluidId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var addAmount = 750.0;
            fluidContainerComponent.AddLiquid(new FluidStack(addAmount, waterFluidId), FluidContainer.Empty);

            // セーブ & ロード
            // Save & Load
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            // ロード後に液体が保持されているか確認
            // Verify fluid persisted after load
            var loadedBlock = loadEnvironment.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsNotNull(loadedBlock);

            var loadedFluidComponent = loadedBlock.GetComponent<TrainPlatformFluidContainerComponent>();
            Assert.IsNotNull(loadedFluidComponent);
            Assert.IsNotNull(loadedFluidComponent.Container);
            Assert.AreEqual(addAmount, loadedFluidComponent.Container.Container.Amount, 0.001);
            Assert.AreEqual(waterFluidId, loadedFluidComponent.Container.Container.FluidId);
        }

        private static int GetArmAnimationTicks(double loadingAnimeSeconds)
        {
            var ticks = GameUpdater.SecondsToTicks(loadingAnimeSeconds);
            return ticks > int.MaxValue ? int.MaxValue : (int)ticks;
        }
    }
}
