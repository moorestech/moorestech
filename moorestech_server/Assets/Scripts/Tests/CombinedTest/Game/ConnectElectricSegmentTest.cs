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
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 0), BlockDirection.North, out var pole1);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(2, 0), BlockDirection.North, out var pole2);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(3, 0), BlockDirection.North, out var pole3);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(-3, 0), BlockDirection.North, out var pole4);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 3), BlockDirection.North, out var pole5);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, -3), BlockDirection.North, out var pole6);
            
            //範囲外の電柱
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(7, 0), BlockDirection.North, out var pole7);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(-7, 0), BlockDirection.North, out var pole8);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 7), BlockDirection.North, out var pole9);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, -7), BlockDirection.North, out var pole10);
            
            IBlock[] poles =
            {
                pole1, pole2, pole3, pole4, pole5, pole6, pole7, pole8, pole9, pole10,
            };
            IBlock[] inRangePoles =
            {
                pole1,
                pole2,
                pole3,
                pole4,
                pole5,
                pole6,
            };
            IBlock[] outOfRangePoles =
            {
                pole7,
                pole8,
                pole9,
                pole10,
            };
            
            IWorldEnergySegmentDatastore<EnergySegment> worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(5, worldElectricSegment.GetEnergySegmentListCount());
            
            var segment = worldElectricSegment.GetEnergySegment(0);
            //電柱を取得する
            IReadOnlyDictionary<BlockInstanceId, IElectricTransformer> electricPoles = segment.EnergyTransformers;
            
            //存在する電柱の数の確認
            //存在している電柱のIDの確認
            foreach (var pole in inRangePoles) Assert.AreEqual(true, electricPoles.ContainsKey(pole.BlockInstanceId));
            
            //存在しない電柱のIDの確認
            foreach (var pole in outOfRangePoles) Assert.AreEqual(false, electricPoles.ContainsKey(pole.BlockInstanceId));
            
            //範囲外同士の接続確認
            //セグメント繋がる位置に電柱を設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(5, 0), BlockDirection.North, out var pole11);
            //セグメントの数を確認
            Assert.AreEqual(4, worldElectricSegment.GetEnergySegmentListCount());
            //マージ後のセグメント、電柱を取得
            segment = worldElectricSegment.GetEnergySegment(3);
            electricPoles = segment.EnergyTransformers;
            //存在する電柱の数の確認
            Assert.AreEqual(8, electricPoles.Count);
            //マージされた電柱のIDの確認
            Assert.AreEqual(true, electricPoles.ContainsKey(pole7.BlockInstanceId));
            Assert.AreEqual(true, electricPoles.ContainsKey(pole11.BlockInstanceId));
        }
        
        //電柱を設置した後に機械、発電機を設置するテスト
        [Test]
        public void PlaceElectricPoleToPlaceMachineTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //起点となる電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 0), BlockDirection.North, out var originElectricPole);
            
            //周りに機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(2, 0), BlockDirection.North, out var inRangeMachine0);
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-2, 0), BlockDirection.North, out var inRangeMachine1);
            //周りに発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 2), BlockDirection.North, out var inRangeGenerator0);
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -2), BlockDirection.North, out var inRangeGenerator1);
            
            //範囲外に機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(3, 0), BlockDirection.North, out var outOfRangeMachine0);
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-3, 0), BlockDirection.North, out var outOfRangeMachine1);
            //範囲外に発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 3), BlockDirection.North, out var outOfRangeGenerator0);
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -3), BlockDirection.North, out var outOfRangeGenerator1);
            
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
            Assert.AreEqual(true, electricBlocks.ContainsKey(inRangeMachine0.BlockInstanceId));
            Assert.AreEqual(true, electricBlocks.ContainsKey(inRangeMachine1.BlockInstanceId));
            Assert.AreEqual(true, powerGeneratorBlocks.ContainsKey(inRangeGenerator0.BlockInstanceId));
            Assert.AreEqual(true, powerGeneratorBlocks.ContainsKey(inRangeGenerator1.BlockInstanceId));
            
            //範囲外の機械、発電機が繋がるように電柱を設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(3, 1), BlockDirection.North, out var pole1);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(1, 3), BlockDirection.North, out var pole2);
            
            segment = segmentDatastore.GetEnergySegment(0);
            electricBlocks = segment.Consumers;
            powerGeneratorBlocks = segment.Generators;
            //存在する機械の数の確認
            Assert.AreEqual(1, segmentDatastore.GetEnergySegmentListCount());
            Assert.AreEqual(3, electricBlocks.Count);
            Assert.AreEqual(3, powerGeneratorBlocks.Count);
            //追加されたIDの確認
            Assert.AreEqual(true, electricBlocks.ContainsKey(outOfRangeMachine0.BlockInstanceId));
            Assert.AreEqual(true, powerGeneratorBlocks.ContainsKey(outOfRangeGenerator0.BlockInstanceId));
        }
        
        //機械、発電機を設置した後に電柱を設置するテスト
        [Test]
        public void PlaceMachineToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            
            //周りに機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(2, 0), BlockDirection.North, out var inRangeMachine0);
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-2, 0), BlockDirection.North, out var inRangeMachine1);
            //周りに発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 2), BlockDirection.North, out var inRangeGenerator0);
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -2), BlockDirection.North, out var inRangeGenerator1);
            
            //範囲外に機械を設置
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(3, 0), BlockDirection.North, out var outOfRangeMachine0);
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(-3, 0), BlockDirection.North, out var outOfRangeMachine1);
            //範囲外に発電機を設置
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, 3), BlockDirection.North, out var outOfRangeGenerator0);
            worldBlockDatastore.TryAddBlock(GenerateId, new Vector3Int(0, -3), BlockDirection.North, out var outOfRangeGenerator1);
            
            //起点となる電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, new Vector3Int(0, 0), BlockDirection.North, out var originPole);
            
            
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
            Assert.AreEqual(true, electricBlocks.ContainsKey(inRangeMachine0.BlockInstanceId));
            Assert.AreEqual(true, electricBlocks.ContainsKey(inRangeMachine1.BlockInstanceId));
            Assert.AreEqual(true, powerGeneratorBlocks.ContainsKey(inRangeGenerator0.BlockInstanceId));
            Assert.AreEqual(true, powerGeneratorBlocks.ContainsKey(inRangeGenerator1.BlockInstanceId));
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