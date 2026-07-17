using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;

namespace Tests.CombinedTest.Game
{
    // 明示接続でセグメントが形成・マージされるか検証
    // Verify that explicit wire connections form and merge energy segments
    public class ConnectElectricSegmentTest
    {
        // 電柱同士を繋ぐと1つのセグメントに統合される
        // Wiring poles together merges them into a single segment
        [Test]
        public void PoleToPoleConnectionMergesIntoSingleSegment()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole1);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole2);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(4, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole3);

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            // 未接続なので3ブロックがそれぞれ独立セグメントを持つ
            // With no wires yet, all three poles are separate segments
            Assert.AreEqual(3, networkDatastore.SegmentCount);

            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(2, 0));
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(4, 0));

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            // 鎖状に繋いだので1セグメントに統合される
            // Chained wiring collapses them into one segment
            Assert.AreEqual(1, networkDatastore.SegmentCount);

            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole1.BlockInstanceId, out var segment));
            var transformers = segment.EnergyTransformers;
            Assert.AreEqual(3, transformers.Count);
            Assert.IsTrue(transformers.ContainsKey(pole1.BlockInstanceId));
            Assert.IsTrue(transformers.ContainsKey(pole2.BlockInstanceId));
            Assert.IsTrue(transformers.ContainsKey(pole3.BlockInstanceId));
        }

        // 電柱に機械・発電機を繋ぐと同一セグメントに登録
        // Wiring machines and generators to a pole registers them as consumers and generators in the same segment
        [Test]
        public void MachineAndGeneratorJoinPoleSegment()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);

            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(2, 0));
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(0, 2));

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(1, networkDatastore.SegmentCount);
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole.BlockInstanceId, out var segment));
            Assert.AreEqual(1, segment.Consumers.Count);
            Assert.AreEqual(1, segment.Generators.Count);
            Assert.IsTrue(segment.Consumers.ContainsKey(machine.BlockInstanceId));
            Assert.IsTrue(segment.Generators.ContainsKey(generator.BlockInstanceId));
        }

        // 2セグメントを電柱で橋渡しすると1つにマージされる
        // Bridging two independently-built segments with a pole merges them into one
        [Test]
        public void BridgingTwoSegmentsMergesConsumersAndGenerators()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();

            // 1つ目のセグメント
            // First segment
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(0, -2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(0, 2));
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(0, -2));

            // 2つ目のセグメント
            // Second segment
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(6, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(6, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(6, -2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole2);
            ElectricWireTestUtil.Connect(Pos(6, 0), Pos(6, 2));
            ElectricWireTestUtil.Connect(Pos(6, 0), Pos(6, -2));

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(2, networkDatastore.SegmentCount);

            // 2つの電柱を橋渡し
            // Bridge the two poles
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(6, 0));

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(1, networkDatastore.SegmentCount);
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole2.BlockInstanceId, out var segment));
            Assert.AreEqual(2, segment.Consumers.Count);
            Assert.AreEqual(2, segment.Generators.Count);
        }

        // ロード後にワイヤー接続が復元されセグメント再形成
        // After save/load the wire connections are restored and the segment reforms
        [Test]
        public void SaveLoadRestoresWiredSegment()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(4, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(2, 0));
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(4, 0));

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            var saveJson = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            // ロード直後のトポロジ反映のため1tick進める
            // Advance one tick after load for the topology flush
            GameUpdater.UpdateOneTick();

            var networkDatastore = loadServiceProvider.GetService<IElectricWireNetworkDatastore>();
            var loadedPole = ServerContext.WorldBlockDatastore.GetBlock(Pos(0, 0));

            Assert.AreEqual(1, networkDatastore.SegmentCount);
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(loadedPole.BlockInstanceId, out var segment));
            Assert.AreEqual(3, segment.EnergyTransformers.Count);
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
