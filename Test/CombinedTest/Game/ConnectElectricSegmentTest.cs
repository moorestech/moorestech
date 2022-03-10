using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Electric;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Test.Module.TestConfig;
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
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();

            //範囲内の電柱
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 3), -3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 4), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 5), 0, -3, BlockDirection.North);

            //範囲外の電柱
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 10), 7, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 11), -7, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 12), 0, 7, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 13), 0, -7, BlockDirection.North);

            var worldElectricSegment = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>();
            //セグメントの数を確認
            Assert.AreEqual(5, worldElectricSegment.GetElectricSegmentListCount());

            var segment = worldElectricSegment.GetElectricSegment(0);
            //電柱を取得する
            var electricPole = segment.GetElectricPoles();

            //存在する電柱の数の確認
            Assert.AreEqual(6, electricPole.Count);
            //存在している電柱のIDの確認
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(i, electricPole[i].GetEntityId());
            }

            //存在しない電柱のIDの確認
            for (int i = 10; i < 13; i++)
            {
                Assert.AreEqual(false, electricPole.ContainsKey(i));
            }

            //範囲外同士の接続確認
            //セグメント繋がる位置に電柱を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 15), 5, 0, BlockDirection.North);
            //セグメントの数を確認
            Assert.AreEqual(4, worldElectricSegment.GetElectricSegmentListCount());
            //マージ後のセグメント、電柱を取得
            segment = worldElectricSegment.GetElectricSegment(3);
            electricPole = segment.GetElectricPoles();
            //存在する電柱の数の確認
            Assert.AreEqual(8, electricPole.Count);
            //マージされた電柱のIDの確認
            Assert.AreEqual(10, electricPole[10].GetEntityId());
            Assert.AreEqual(15, electricPole[15].GetEntityId());
        }

        //電柱を設置した後に機械、発電機を設置するテスト
        [Test]
        public void PlaceElectricPoleToPlaceMachineTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
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

            var segmentDatastore = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>();
            //範囲内の設置
            var segment = segmentDatastore.GetElectricSegment(0);
            //機械、発電機を取得する
            var electricBlocks = segment.GetElectrics();
            var powerGeneratorBlocks = segment.GetGenerators();


            //存在する機械の数の確認
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1, electricBlocks[1].GetEntityId());
            Assert.AreEqual(2, electricBlocks[2].GetEntityId());
            Assert.AreEqual(3, powerGeneratorBlocks[3].GetEntityId());
            Assert.AreEqual(4, powerGeneratorBlocks[4].GetEntityId());

            //範囲外の機械、発電機が繋がるように電柱を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 20), 3, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 21), 1, 3, BlockDirection.North);

            segment = segmentDatastore.GetElectricSegment(0);
            electricBlocks = segment.GetElectrics();
            powerGeneratorBlocks = segment.GetGenerators();
            //存在する機械の数の確認
            Assert.AreEqual(1, segmentDatastore.GetElectricSegmentListCount());
            Assert.AreEqual(3, electricBlocks.Count);
            Assert.AreEqual(3, powerGeneratorBlocks.Count);
            //追加されたIDの確認
            Assert.AreEqual(10, electricBlocks[10].GetEntityId());
            Assert.AreEqual(12, powerGeneratorBlocks[12].GetEntityId());
        }

        //機械、発電機を設置した後に電柱を設置するテスト
        [Test]
        public void PlaceMachineToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
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
            var electricBlocks = (Dictionary<int, IBlockElectric>) typeof(ElectricSegment).GetField("_electrics",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(segment);
            var powerGeneratorBlocks = (Dictionary<int, IPowerGenerator>) typeof(ElectricSegment)
                .GetField("_generators",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(segment);


            //存在する機械の数の確認
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1, electricBlocks[1].GetEntityId());
            Assert.AreEqual(2, electricBlocks[2].GetEntityId());
            Assert.AreEqual(3, powerGeneratorBlocks[3].GetEntityId());
            Assert.AreEqual(4, powerGeneratorBlocks[4].GetEntityId());
        }

        //別々のセグメント同士を電柱でつなぐテスト
        [Test]
        public void SegmentConnectionTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();

            //一つ目のセグメントを設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            //周りに機械と発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 2), -2, 0, BlockDirection.North);

            //二つ目のセグメントを設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 10), 6, 0, BlockDirection.North);
            //周りに機械と発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 3), 7, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(PowerGenerateId, 4), 7, 1, BlockDirection.North);

            var segmentDatastore = saveServiceProvider.GetService<IWorldElectricSegmentDatastore>();
            //セグメントの数を確認
            Assert.AreEqual(2, segmentDatastore.GetElectricSegmentListCount());

            //セグメント同士をつなぐ電柱を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 20), 3, 0, BlockDirection.North);
            //セグメントの数を確認
            Assert.AreEqual(1, segmentDatastore.GetElectricSegmentListCount());
            //セグメントを取得
            var segment = segmentDatastore.GetElectricSegment(0);
            //機械、発電機の数を確認
            Assert.AreEqual(2, segment.GetElectrics().Count);
            Assert.AreEqual(2, segment.GetGenerators().Count);
        }
    }
}