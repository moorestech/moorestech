using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainCargoPlatformTransferModeSaveLoadTest
    {
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
            environment.WorldBlockDatastore.RemoveBlock(loadPosition);
            environment.WorldBlockDatastore.RemoveBlock(unloadPosition);

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
    }
}
