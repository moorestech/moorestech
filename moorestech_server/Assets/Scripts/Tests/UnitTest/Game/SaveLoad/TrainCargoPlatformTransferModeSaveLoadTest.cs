using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainCargoPlatformTransferModeSaveLoadTest
    {
        [Test]
        public void CargoPlatformAnimationProgressPersistsAcrossSaveLoad()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var (cargoBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(cargoBlock, "貨物プラットフォームの生成に失敗しました。");
            Assert.IsNotNull(railComponents, "貨物プラットフォームのRailComponent取得に失敗しました。");

            var cargoPlatformComponent = cargoBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoPlatformComponent, "CargoplatformComponentの取得に失敗しました。");
            Assert.IsTrue(cargoBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var cargoInventory), "貨物プラットフォームのインベントリ取得に失敗しました。");

            var cargoParam = (TrainCargoPlatformBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainCargoPlatform).BlockParam;
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            cargoInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = cargoBlock.BlockPositionInfo.BlockSize.z;
            var railPosition = new RailPosition(new List<IRailNode> { exitNode, entryNode }, segmentLength, 0);
            var firstTrainCarMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First();
            var trainCar = new TrainCar(firstTrainCarMaster, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            trainUnit.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(trainCar.IsDocked, "列車が貨物プラットフォームにドッキングしていません。");

            var totalTicks = GetArmAnimationTicks(cargoParam.LoadingAnimeSpeed);
            var elapsedTicks = totalTicks / 2;
            for (var i = 0; i < elapsedTicks; i++) cargoPlatformComponent.Update();
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "セーブ前に一括移送が発生しています。");

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            var loadedBlock = loadEnvironment.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsNotNull(loadedBlock, "ロード後に貨物プラットフォームが見つかりません。");
            var loadedComponent = loadedBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(loadedComponent, "ロード後にCargoplatformComponentが見つかりません。");
            Assert.IsTrue(loadedBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var loadedInventory), "ロード後の貨物インベントリ取得に失敗しました。");

            var loadedTrain = loadEnvironment.GetTrainUpdateService().GetRegisteredTrains().Single();
            var loadedCar = loadedTrain.Cars[0];
            Assert.IsTrue(loadedCar.IsDocked, "ロード後に列車ドッキング状態が復元されていません。");

            var remainingTicks = totalTicks + 1 - elapsedTicks;
            for (var i = 0; i < remainingTicks; i++) loadedComponent.Update();

            var platformStack = loadedInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, platformStack.Id, "ロード後にアニメーション進捗が復元されず、予定tickで一括移送されませんでした。");
            var carStack = loadedCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Id, "ロード後に列車側へアイテムが移送されていません。");
            Assert.AreEqual(maxStack, carStack.Count, "ロード後に列車側へ全量移送されていません。");

            loadedTrain.trainUnitStationDocking.UndockFromStation();
            loadEnvironment.GetTrainDiagramManager().UnregisterDiagram(loadedTrain.trainDiagram);
            loadEnvironment.GetTrainUpdateService().UnregisterTrain(loadedTrain);
        }

        [Test]
        public void CargoPlatformTransferModesPersistAcrossSaveLoad()
        {
            // セーブ対象の駅ブロックを生成する
            // Create the station blocks to be saved
            var environment = TrainTestHelper.CreateEnvironment();
            var loadPosition = new Vector3Int(12, 0, 0);
            var unloadPosition = new Vector3Int(28, 0, 0);

            var loadBlock = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainCargoPlatform, loadPosition, BlockDirection.North);
            Assert.IsNotNull(loadBlock, "Loadモード側の貨物プラットフォーム生成に失敗しました。");

            var unloadBlock = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainCargoPlatform, unloadPosition, BlockDirection.North);
            Assert.IsNotNull(unloadBlock, "Unloadモード側の貨物プラットフォーム生成に失敗しました。");

            var loadCargo = loadBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(loadCargo, "Loadモード側のCargoplatformComponent取得に失敗しました。");

            var unloadCargo = unloadBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(unloadCargo, "Unloadモード側のCargoplatformComponent取得に失敗しました。");

            loadCargo!.SetTransferMode(CargoplatformComponent.CargoTransferMode.LoadToTrain);
            unloadCargo!.SetTransferMode(CargoplatformComponent.CargoTransferMode.UnloadToPlatform);

            // セーブデータを生成して環境を解体する
            // Build the save data and tear down the environment
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            environment.WorldBlockDatastore.RemoveBlock(loadPosition, BlockRemoveReason.ManualRemove);
            environment.WorldBlockDatastore.RemoveBlock(unloadPosition, BlockRemoveReason.ManualRemove);

            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            // ロード後の駅ブロック状態を検証する
            // Verify station block states after load
            var loadedLoadBlock = loadEnvironment.WorldBlockDatastore.GetBlock(loadPosition);
            Assert.IsNotNull(loadedLoadBlock, "ロード後にLoadモード側の貨物プラットフォームが見つかりません。");

            var loadedUnloadBlock = loadEnvironment.WorldBlockDatastore.GetBlock(unloadPosition);
            Assert.IsNotNull(loadedUnloadBlock, "ロード後にUnloadモード側の貨物プラットフォームが見つかりません。");

            var loadedLoadCargo = loadedLoadBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(loadedLoadCargo, "ロード後にLoadモード側のCargoplatformComponentが見つかりません。");

            var loadedUnloadCargo = loadedUnloadBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(loadedUnloadCargo, "ロード後にUnloadモード側のCargoplatformComponentが見つかりません。");

            Assert.AreEqual(CargoplatformComponent.CargoTransferMode.LoadToTrain, loadedLoadCargo!.TransferMode,
                "Loadモード側の貨物プラットフォームの転送モードが復元されていません。");

            Assert.AreEqual(CargoplatformComponent.CargoTransferMode.UnloadToPlatform, loadedUnloadCargo!.TransferMode,
                "Unloadモード側の貨物プラットフォームの転送モードが復元されていません。");
        }

        private static int GetArmAnimationTicks(double loadingAnimeSeconds)
        {
            var ticks = GameUpdater.SecondsToTicks(loadingAnimeSeconds);
            return ticks > int.MaxValue ? int.MaxValue : (int)ticks;
        }
    }
}
