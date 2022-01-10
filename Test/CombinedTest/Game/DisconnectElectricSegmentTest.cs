using Core.Block.BlockFactory;
using Core.Electric;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;

namespace Test.CombinedTest.Game
{
    //電柱が無くなったときにセグメントが切断されるテスト
    public class DisconnectElectricSegmentTest
    {
        private const int ElectricPoleId = 4;
        private const int MachineId = 1;
        private const int PowerGenerateId = 5;
        
        [Test]
        public void RemoveElectricPoleToDisconnectSegment()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            /*設置する電柱、機械、発電機の場所
             * M □  □ G □  □ M
             * P □  □ P □  □ P
             * G
             */
            
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();
            
            //電柱の設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 6, 0, BlockDirection.North);
            
            //発電機と機械の設定
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 3), 0, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 4), 0, -1, BlockDirection.North);
            
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 5), 3, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 6), 6, 1, BlockDirection.North);
            
            var worldElectricSegment = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>();
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetElectricSegmentListCount());
            
            //真ん中の電柱を削除
            worldBlockDatastore.RemoveBlock(3, 0);
            //セグメントが増えていることを確認する
            Assert.AreEqual(2, worldElectricSegment.GetElectricSegmentListCount());
    
            //真ん中の発電機が2つのセグメントにないことを確認する
            Assert.AreEqual(false,worldElectricSegment.GetElectricSegment(0).GetGenerators().ContainsKey(5));
            Assert.AreEqual(false,worldElectricSegment.GetElectricSegment(1).GetGenerators().ContainsKey(5));
            
            //両端の電柱が別のセグメントであることを確認する
            var segment1 = worldElectricSegment.GetElectricSegment(worldBlockDatastore.GetBlock(0,0) as IElectricPole);
            var segment2 = worldElectricSegment.GetElectricSegment(worldBlockDatastore.GetBlock(6,0) as IElectricPole);
            
            Assert.AreNotEqual(segment1.GetHashCode(), segment2.GetHashCode());
            
            




        }
    }
}