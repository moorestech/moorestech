using Core.Block.Blocks;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;

namespace Test.UnitTest.Game.SaveLoad
{
    public class AssembleSaveJsonTextTest
    {
        //何もデータがない時のテスト
        [Test]
        public void NoneTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var json = assembleSaveJsonText.AssembleSaveJson();
            Assert.AreEqual("{\"world\":[],\"playerInventory\":[]}", json);
        }

        //ブロックを追加した時のテスト
        [Test]
        public void SimpleBlockPlacedTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            worldBlockDatastore.AddBlock(new VanillaBlock(10, 10), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(new VanillaBlock(15, 100), 10, -15, BlockDirection.North);

            var json = assembleSaveJsonText.AssembleSaveJson();

            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            (loadServiceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);
            
            var worldLoadBlockDatastore = loadServiceProvider.GetService<IWorldBlockDatastore>();

            var block1 = worldLoadBlockDatastore.GetBlock(0, 0);
            Assert.AreEqual(10, block1.GetBlockId());
            Assert.AreEqual(10, block1.GetEntityId());

            var block2 = worldLoadBlockDatastore.GetBlock(10, -15);
            Assert.AreEqual(15, block2.GetBlockId());
            Assert.AreEqual(100, block2.GetEntityId());
        }
    }
}