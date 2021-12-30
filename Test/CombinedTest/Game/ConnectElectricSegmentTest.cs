using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Electric;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using World.DataStore;

namespace Test.CombinedTest.Game
{
    public class ConnectElectricSegmentTest
    {
        //ブロックIDが変わったらここを変える
        private const int ElectricPoleId = 4;
        
        [Test]
        public void ElectricPoleToElectricPoleTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();

            //範囲内の電柱
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0),0,0,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1),2,0,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2),3,0,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 3),-3,0,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 4),0,3,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 5),0,-3,BlockDirection.North);
            
            //範囲外の電柱
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 10),7,0,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 11),-7,0,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 12),0,7,BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 13),0,-7,BlockDirection.North);

            var segment = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>().GetElectricSegment(0);
            //リフレクションで電柱を取得する
            var electricPole = (Dictionary<int,IElectricPole>)typeof(ElectricSegment).GetField("_electricPoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(segment);
            
            //存在する電柱の数の確認
            Assert.AreEqual(6,electricPole.Count);
            //存在している電柱のIDの確認
            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(i,electricPole[i].GetIntId());
            }
            
            //存在しない電柱のIDの確認
            for (int i = 10; i < 14; i++)
            {
                Assert.AreEqual(false,electricPole.ContainsKey(i));
            }
        }
    }
}