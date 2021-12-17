using System;
using Core.Block;
using Core.Block.BlockFactory;
using Game.Save.Json;
using Game.World.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using World;
using World.Event;

namespace Test.UnitTest.Game
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
            Assert.AreEqual("{\"world\":[],\"inventory\":[]}",json);
        }
        
        [Test]
        public void BlockPlacedTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            worldBlockDatastore.AddBlock(new NormalBlock( 10, 10), 0, 0);
            worldBlockDatastore.AddBlock(new NormalBlock( 15, 100), 10,-15);
            
            var json = assembleSaveJsonText.AssembleSaveJson();
            
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            loadServiceProvider.GetService<AssembleSaveJsonText>().LoadJson(json);
            var worldLoadBlockDatastore = loadServiceProvider.GetService<IWorldBlockDatastore>();
            
            var block1 = worldLoadBlockDatastore.GetBlock(0, 0);
            Assert.AreEqual(10, block1.GetBlockId());
            Assert.AreEqual(10, block1.GetIntId());
            
            var block2 = worldLoadBlockDatastore.GetBlock(10, -15);
            Assert.AreEqual(15, block2.GetBlockId());
            Assert.AreEqual(100, block2.GetIntId());

        }
        
    }
}