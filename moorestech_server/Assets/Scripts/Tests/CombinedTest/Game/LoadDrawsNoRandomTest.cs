using Core.Update;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    // ロード中にIDを採番すると乱数列が保存時とずれ、巻き戻せばロード後の採番がセーブ済みIDと衝突する
    // Allocating ids during load desynchronizes the random stream, and rewinding it makes later ids collide with saved ones
    public class LoadDrawsNoRandomTest
    {
        [Test]
        public void レールブロックのロードは乱数を引かずノードGUIDを引き継ぐ()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(10, 0, 10);
            var (_, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(env, ForUnitTestModBlockId.TestTrainRail, position, BlockDirection.North);
            var savedFrontNodeGuid = railComponents[0].FrontNode.Guid;
            var savedBackNodeGuid = railComponents[0].BackNode.Guid;

            var json = env.ServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();
            var savedRandomState = GameRandom.ExportState();

            var loadEnv = TrainTestHelper.CreateEnvironment();
            (loadEnv.ServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);

            CollectionAssert.AreEqual(savedRandomState, GameRandom.ExportState(), "ロード中に乱数を引いている");

            var loadedRailComponent = loadEnv.WorldBlockDatastore.GetBlock(position).GetComponent<RailComponent>();
            Assert.AreEqual(savedFrontNodeGuid, loadedRailComponent.FrontNode.Guid, "前ノードのGUIDが復元されていない");
            Assert.AreEqual(savedBackNodeGuid, loadedRailComponent.BackNode.Guid, "後ノードのGUIDが復元されていない");
        }
    }
}
