using System.Collections.Generic;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class ConnectElectricSegmentTest
    {
        //ブロックIDが変わったらここを変える
        private const int ElectricPoleId = ForUnitTestModBlockId.ElectricPoleId;
        private const int MachineId = ForUnitTestModBlockId.MachineId;
        private const int GenerateId = ForUnitTestModBlockId.GeneratorId;
        
        //電柱を設置し、電柱に接続するテスト
        [Test]
        public void PlaceElectricPoleToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //範囲内の電柱
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 0), BlockDirection.North, out _, new BlockInstanceId(0));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(2, 0), BlockDirection.North, out _, new BlockInstanceId(1));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(3, 0), BlockDirection.North, out _, new BlockInstanceId(2));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(-3, 0), BlockDirection.North, out _, new BlockInstanceId(3));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 3), BlockDirection.North, out _, new BlockInstanceId(4));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, -3), BlockDirection.North, out _, new BlockInstanceId(5));
            
            //範囲外の電柱
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(7, 0), BlockDirection.North, out _, new BlockInstanceId(10));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(-7, 0), BlockDirection.North, out _, new BlockInstanceId(11));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 7), BlockDirection.North, out _, new BlockInstanceId(12));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, -7), BlockDirection.North, out _, new BlockInstanceId(13));
            
            IWorldEnergySegmentDatastore<EnergySegment> worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(5, worldElectricSegment.GetEnergySegmentListCount());
            
            var segment = worldElectricSegment.GetEnergySegment(0);
            //電柱を取得する
            IReadOnlyDictionary<BlockInstanceId, IElectricTransformer> electricPoles = segment.EnergyTransformers;
            
            //存在する電柱の数の確認
            Assert.AreEqual(6, electricPoles.Count);
            //存在している電柱のIDの確認
            for (var i = 0; i < 6; i++) Assert.AreEqual(i, electricPoles[new BlockInstanceId(i)].BlockInstanceId.AsPrimitive());
            
            //存在しない電柱のIDの確認
            for (var i = 10; i < 13; i++) Assert.AreEqual(false, electricPoles.ContainsKey(new BlockInstanceId(i)));
            
            //範囲外同士の接続確認
            //セグメント繋がる位置に電柱を設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(5, 0), BlockDirection.North, out _, new BlockInstanceId(15));
            //セグメントの数を確認
            Assert.AreEqual(4, worldElectricSegment.GetEnergySegmentListCount());
            //マージ後のセグメント、電柱を取得
            segment = worldElectricSegment.GetEnergySegment(3);
            electricPoles = segment.EnergyTransformers;
            //存在する電柱の数の確認
            Assert.AreEqual(8, electricPoles.Count);
            //マージされた電柱のIDの確認
            Assert.AreEqual(10, electricPoles[new BlockInstanceId(10)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(15, electricPoles[new BlockInstanceId(15)].BlockInstanceId.AsPrimitive());
        }
        
        //電柱を設置した後に機械、発電機を設置するテスト
        [Test]
        public void PlaceElectricPoleToPlaceMachineTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //起点となる電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 0), BlockDirection.North, out _, new BlockInstanceId(0));
            
            //周りに機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(2, 0), BlockDirection.North, out _, new BlockInstanceId(1));
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-2, 0), BlockDirection.North, out _, new BlockInstanceId(2));
            //周りに発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 2), BlockDirection.North, out _, new BlockInstanceId(3));
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -2), BlockDirection.North, out _, new BlockInstanceId(4));
            
            //範囲外に機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(3, 0), BlockDirection.North, out _, new BlockInstanceId(10));
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-3, 0), BlockDirection.North, out _, new BlockInstanceId(11));
            //範囲外に発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 3), BlockDirection.North, out _, new BlockInstanceId(12));
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -3), BlockDirection.North, out _, new BlockInstanceId(13));
            
            IWorldEnergySegmentDatastore<EnergySegment> segmentDatastore = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //範囲内の設置
            var segment = segmentDatastore.GetEnergySegment(0);
            //機械、発電機を取得する
            IReadOnlyDictionary<BlockInstanceId, IElectricConsumer> electricBlocks = segment.Consumers;
            IReadOnlyDictionary<BlockInstanceId, IElectricGenerator> powerGeneratorBlocks = segment.Generators;
            
            
            //存在する機械の数の確認
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1, electricBlocks[new BlockInstanceId(1)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(2, electricBlocks[new BlockInstanceId(2)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(3, powerGeneratorBlocks[new BlockInstanceId(3)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(4, powerGeneratorBlocks[new BlockInstanceId(4)].BlockInstanceId.AsPrimitive());
            
            //範囲外の機械、発電機が繋がるように電柱を設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(3, 1), BlockDirection.North, out _, new BlockInstanceId(20));
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(1, 3), BlockDirection.North, out _, new BlockInstanceId(21));
            
            segment = segmentDatastore.GetEnergySegment(0);
            electricBlocks = segment.Consumers;
            powerGeneratorBlocks = segment.Generators;
            //存在する機械の数の確認
            Assert.AreEqual(1, segmentDatastore.GetEnergySegmentListCount());
            Assert.AreEqual(3, electricBlocks.Count);
            Assert.AreEqual(3, powerGeneratorBlocks.Count);
            //追加されたIDの確認
            Assert.AreEqual(10, electricBlocks[new BlockInstanceId(10)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(12, powerGeneratorBlocks[new BlockInstanceId(12)].BlockInstanceId.AsPrimitive());
        }
        
        //機械、発電機を設置した後に電柱を設置するテスト
        [Test]
        public void PlaceMachineToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            
            //周りに機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(2, 0), BlockDirection.North, out _, new BlockInstanceId(1));
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-2, 0), BlockDirection.North, out _, new BlockInstanceId(2));
            //周りに発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 2), BlockDirection.North, out _, new BlockInstanceId(3));
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -2), BlockDirection.North, out _, new BlockInstanceId(4));
            
            //範囲外に機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(3, 0), BlockDirection.North, out _, new BlockInstanceId(10));
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-3, 0), BlockDirection.North, out _, new BlockInstanceId(11));
            //範囲外に発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 3), BlockDirection.North, out _, new BlockInstanceId(12));
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -3), BlockDirection.North, out _, new BlockInstanceId(13));
            
            //起点となる電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 0), BlockDirection.North, out _, new BlockInstanceId(0));
            
            
            //範囲内の設置
            var segment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>()
                .GetEnergySegment(0);
            //リフレクションで機械を取得する
            IReadOnlyDictionary<BlockInstanceId, IElectricConsumer> electricBlocks = segment.Consumers;
            IReadOnlyDictionary<BlockInstanceId, IElectricGenerator> powerGeneratorBlocks = segment.Generators;
            
            
            //存在する機械の数の確認
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //存在している機械のIDの確認
            Assert.AreEqual(1, electricBlocks[new BlockInstanceId(1)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(2, electricBlocks[new BlockInstanceId(2)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(3, powerGeneratorBlocks[new BlockInstanceId(3)].BlockInstanceId.AsPrimitive());
            Assert.AreEqual(4, powerGeneratorBlocks[new BlockInstanceId(4)].BlockInstanceId.AsPrimitive());
        }
        
        //別々のセグメント同士を電柱でつなぐテスト
        [Test]
        public void SegmentConnectionTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //一つ目のセグメントを設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 0), BlockDirection.North, out _);
            //周りに機械と発電機を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(2, 0), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -2), BlockDirection.North, out _);
            
            //二つ目のセグメントを設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(6, 0), BlockDirection.North, out _);
            //周りに機械と発電機を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(7, 0), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(7, 1), BlockDirection.North, out _);
            
            IWorldEnergySegmentDatastore<EnergySegment> segmentDatastore = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(2, segmentDatastore.GetEnergySegmentListCount());
            
            //セグメント同士をつなぐ電柱を設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(3, 0), BlockDirection.North, out _);
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