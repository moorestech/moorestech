using System;
using Core.Block.Blocks;
using Core.Block.Config;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.StartServerSystem;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.UnitTest.Game.SaveLoad
{
    public class AssembleSaveJsonTextTest
    {
        //何もデータがない時のテスト
        [Test]
        public void NoneTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var json = assembleSaveJsonText.AssembleSaveJson();
            Assert.AreEqual("{\"world\":[],\"playerInventory\":[]}", json);
        }

        //ブロックを追加した時のテスト
        [Test]
        public void SimpleBlockPlacedTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();

            var blockHash1 = blockConfig.GetBlockConfig(1).BlockHash;
            var blockHash2 = blockConfig.GetBlockConfig(2).BlockHash;

            worldBlockDatastore.AddBlock(new VanillaBlock(1, 10,blockHash1), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(new VanillaBlock(2, 100,blockHash2), 10, -15, BlockDirection.North);

            var json = assembleSaveJsonText.AssembleSaveJson();

            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);
            
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