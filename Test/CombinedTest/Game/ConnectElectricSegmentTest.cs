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
        private const int MachineId = 1;
        private const int PowerGenerateId = 5;
        
        //電柱を設置し、電柱に接続するテスト
        [Test]
        public void PlaceElectricPoleToPlaceElectricPoleTest()
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

            var worldElectricSegment = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>();
            //セグメントの数を確認
            Assert.AreEqual(5, worldElectricSegment.GetElectricSegmentListCount());
            
            var segment = worldElectricSegment.GetElectricSegment(0);
            //リフレクションで電柱を取得する
            var electricPole = (Dictionary<int,IElectricPole>)typeof(ElectricSegment).GetField("_electricPoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(segment);
            
            //存在する電柱の数の確認
            Assert.AreEqual(6,electricPole.Count);
            //存在している電柱のIDの確認
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(i,electricPole[i].GetIntId());
            }
            
            //存在しない電柱のIDの確認
            for (int i = 10; i < 13; i++)
            {
                Assert.AreEqual(false,electricPole.ContainsKey(i));
            }
        }
        
        //電柱を設置した後に機械、発電機を設置するテスト
        [Test]
        public void PlaceElectricPoleToPlaceMachineTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();

            //起点となる電柱の設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            
            //周りに機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 2), -2, 0, BlockDirection.North);
            //周りに発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 3), 0, 2, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 4), 0, -2, BlockDirection.North);

            //範囲外に機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 10), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 11), -3, 0, BlockDirection.North);
            //範囲外に発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 12), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 13), 0, -3, BlockDirection.North);

            //範囲内の設置
            var segment = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>().GetElectricSegment(0);
            //リフレクションで機械を取得する
            var electricBlocks = (Dictionary<int,IBlockElectric>)typeof(ElectricSegment).GetField("_electrics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(segment);
            var powerGeneratorBlocks = (Dictionary<int,IPowerGenerator>)typeof(ElectricSegment).GetField("_generators", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(segment);
            
            
            //存在する機械の数の確認
            Assert.AreEqual(2,electricBlocks.Count);
            Assert.AreEqual(2,powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1,electricBlocks[1].GetIntId());
            Assert.AreEqual(2,electricBlocks[2].GetIntId());
            Assert.AreEqual(3,powerGeneratorBlocks[3].GetIntId());
            Assert.AreEqual(4,powerGeneratorBlocks[4].GetIntId());
        }
        
        //機械、発電機を設置した後に電柱を設置するテスト
        [Test]
        public void PlaceMachineToPlaceElectricPoleTest()
        {
            
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();

            
            //周りに機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 2), -2, 0, BlockDirection.North);
            //周りに発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 3), 0, 2, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 4), 0, -2, BlockDirection.North);

            //範囲外に機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 10), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 11), -3, 0, BlockDirection.North);
            //範囲外に発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 12), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 13), 0, -3, BlockDirection.North);
            
            
            
            //起点となる電柱の設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            
            

            //範囲内の設置
            var segment = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>().GetElectricSegment(0);
            //リフレクションで機械を取得する
            var electricBlocks = (Dictionary<int,IBlockElectric>)typeof(ElectricSegment).GetField("_electrics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(segment);
            var powerGeneratorBlocks = (Dictionary<int,IPowerGenerator>)typeof(ElectricSegment).GetField("_generators", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(segment);
            
            
            //存在する機械の数の確認
            Assert.AreEqual(2,electricBlocks.Count);
            Assert.AreEqual(2,powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1,electricBlocks[1].GetIntId());
            Assert.AreEqual(2,electricBlocks[2].GetIntId());
            Assert.AreEqual(3,powerGeneratorBlocks[3].GetIntId());
            Assert.AreEqual(4,powerGeneratorBlocks[4].GetIntId());
        }
    }
}