using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class AssembleSaveJsonTextTest
    {
        //ブロックを追加した時のテスト
        [Test]
        public void SimpleBlockPlacedTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();

            var blockHash1 = blockConfig.GetBlockConfig(1).BlockHash;
            var blockHash2 = blockConfig.GetBlockConfig(2).BlockHash;

            worldBlockDatastore.AddBlock(blockFactory.Create(1, 10), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(2, 100), 10, -15, BlockDirection.North);

            var json = assembleSaveJsonText.AssembleSaveJson();

            Debug.Log(json);

            var (_, loadServiceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);

            var worldLoadBlockDatastore = loadServiceProvider.GetService<IWorldBlockDatastore>();

            var block1 = worldLoadBlockDatastore.GetBlock(0, 0);
            Assert.AreEqual(1, block1.BlockId);
            Assert.AreEqual(10, block1.EntityId);

            var block2 = worldLoadBlockDatastore.GetBlock(10, -15);
            Assert.AreEqual(2, block2.BlockId);
            Assert.AreEqual(100, block2.EntityId);
        }
    }
}