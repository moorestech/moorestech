using Core.EnergySystem;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class ConnectElectricSegmentTest
    {
        //ブロックIDが変わったらここを変える
        private const int ElectricPoleId = UnitTestModBlockId.ElectricPoleId;
        private const int MachineId = UnitTestModBlockId.MachineId;
        private const int GenerateId = UnitTestModBlockId.GeneratorId;

        //電柱を設置し、電柱に接続するテスト
        [Test]
        public void PlaceElectricPoleToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

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

            var worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(5, worldElectricSegment.GetEnergySegmentListCount());

            var segment = worldElectricSegment.GetEnergySegment(0);
            //電柱を取得する
            var electricPoles = segment.EnergyTransformers;

            //存在する電柱の数の確認
            Assert.AreEqual(6, electricPoles.Count);
            //存在している電柱のIDの確認
            for (var i = 0; i < 6; i++) Assert.AreEqual(i, electricPoles[i].EntityId);

            //存在しない電柱のIDの確認
            for (var i = 10; i < 13; i++) Assert.AreEqual(false, electricPoles.ContainsKey(i));

            //範囲外同士の接続確認
            //セグメント繋がる位置に電柱を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 15), 5, 0, BlockDirection.North);
            //セグメントの数を確認
            Assert.AreEqual(4, worldElectricSegment.GetEnergySegmentListCount());
            //マージ後のセグメント、電柱を取得
            segment = worldElectricSegment.GetEnergySegment(3);
            electricPoles = segment.EnergyTransformers;
            //存在する電柱の数の確認
            Assert.AreEqual(8, electricPoles.Count);
            //マージされた電柱のIDの確認
            Assert.AreEqual(10, electricPoles[10].EntityId);
            Assert.AreEqual(15, electricPoles[15].EntityId);
        }

        //電柱を設置した後に機械、発電機を設置するテスト
        [Test]
        public void PlaceElectricPoleToPlaceMachineTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            //起点となる電柱の設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);

            //周りに機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 2), -2, 0, BlockDirection.North);
            //周りに発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 3), 0, 2, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 0, -2, BlockDirection.North);

            //範囲外に機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 10), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 11), -3, 0, BlockDirection.North);
            //範囲外に発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 12), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 13), 0, -3, BlockDirection.North);

            var segmentDatastore = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //範囲内の設置
            var segment = segmentDatastore.GetEnergySegment(0);
            //機械、発電機を取得する
            var electricBlocks = segment.Consumers;
            var powerGeneratorBlocks = segment.Generators;


            //存在する機械の数の確認
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1, electricBlocks[1].EntityId);
            Assert.AreEqual(2, electricBlocks[2].EntityId);
            Assert.AreEqual(3, powerGeneratorBlocks[3].EntityId);
            Assert.AreEqual(4, powerGeneratorBlocks[4].EntityId);

            //範囲外の機械、発電機が繋がるように電柱を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 20), 3, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 21), 1, 3, BlockDirection.North);

            segment = segmentDatastore.GetEnergySegment(0);
            electricBlocks = segment.Consumers;
            powerGeneratorBlocks = segment.Generators;
            //存在する機械の数の確認
            Assert.AreEqual(1, segmentDatastore.GetEnergySegmentListCount());
            Assert.AreEqual(3, electricBlocks.Count);
            Assert.AreEqual(3, powerGeneratorBlocks.Count);
            //追加されたIDの確認
            Assert.AreEqual(10, electricBlocks[10].EntityId);
            Assert.AreEqual(12, powerGeneratorBlocks[12].EntityId);
        }

        //機械、発電機を設置した後に電柱を設置するテスト
        [Test]
        public void PlaceMachineToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();


            //周りに機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 2), -2, 0, BlockDirection.North);
            //周りに発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 3), 0, 2, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 0, -2, BlockDirection.North);

            //範囲外に機械を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 10), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 11), -3, 0, BlockDirection.North);
            //範囲外に発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 12), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 13), 0, -3, BlockDirection.North);


            //起点となる電柱の設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);


            //範囲内の設置
            var segment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>()
                .GetEnergySegment(0);
            //リフレクションで機械を取得する
            var electricBlocks = segment.Consumers;
            var powerGeneratorBlocks = segment.Generators;


            //存在する機械の数の確認
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1, electricBlocks[1].EntityId);
            Assert.AreEqual(2, electricBlocks[2].EntityId);
            Assert.AreEqual(3, powerGeneratorBlocks[3].EntityId);
            Assert.AreEqual(4, powerGeneratorBlocks[4].EntityId);
        }

        //別々のセグメント同士を電柱でつなぐテスト
        [Test]
        public void SegmentConnectionTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            //一つ目のセグメントを設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            //周りに機械と発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 2), -2, 0, BlockDirection.North);

            //二つ目のセグメントを設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 10), 6, 0, BlockDirection.North);
            //周りに機械と発電機を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 3), 7, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 7, 1, BlockDirection.North);

            var segmentDatastore = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(2, segmentDatastore.GetEnergySegmentListCount());

            //セグメント同士をつなぐ電柱を設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 20), 3, 0, BlockDirection.North);
            //セグメントの数を確認
            Assert.AreEqual(1, segmentDatastore.GetEnergySegmentListCount());
            //セグメントを取得
            var segment = segmentDatastore.GetEnergySegment(0);
            //機械、発電機の数を確認
            Assert.AreEqual(2, segment.Consumers.Count);
            Assert.AreEqual(2, segment.Generators.Count);
        }
    }
}