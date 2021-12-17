using System;
using Core.Block;
using Core.Block.BlockFactory;
using Game.Save.Json;
using Game.World.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;

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
            var ans =
                "{\"world\":[{\"X\":0,\"Y\":0,\"id\":10,\"intId\":10,\"state\":\"\"},{\"X\":10,\"Y\":-15,\"id\":15,\"intId\":100,\"state\":\"\"}],\"inventory\":[]}";
            Assert.AreEqual(ans,json);
        }
        
    }
}