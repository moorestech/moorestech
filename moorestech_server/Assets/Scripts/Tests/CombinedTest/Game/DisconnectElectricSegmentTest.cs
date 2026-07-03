using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;

namespace Tests.CombinedTest.Game
{
    // ブロック削除でセグメントが分割・縮小されるか検証
    // Verify that removing a wired block correctly splits and shrinks energy segments
    public class DisconnectElectricSegmentTest
    {
        // 鎖状の中間電柱を削除すると2セグメントに分割
        // Removing the middle pole of a chained segment splits it into two segments
        [Test]
        public void RemoveMiddlePoleSplitsSegment()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole1);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(4, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole3);
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(2, 0));
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(4, 0));

            Assert.AreEqual(1, networkDatastore.SegmentCount);

            // 中間電柱を削除して鎖を断つ
            // Remove the middle pole to break the chain
            worldBlockDatastore.RemoveBlock(Pos(2, 0), BlockRemoveReason.ManualRemove);

            Assert.AreEqual(2, networkDatastore.SegmentCount);

            // 両端の電柱が別セグメントに属することを確認
            // Confirm the two end poles now belong to different segments
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole1.BlockInstanceId, out var segment1));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole3.BlockInstanceId, out var segment3));
            Assert.AreNotSame(segment1, segment3);
        }

        // 電柱削除で機械・発電機は各自単独セグメントになる
        // Removing the pole splits the connected machine and generator into their own single-block segments
        [Test]
        public void RemovePoleDisconnectsMachineAndGenerator()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(4, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(0, 0));
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(4, 0));

            // 機械・発電機・電柱が1セグメントに集約されている
            // The machine, generator, and pole are consolidated into one segment
            Assert.AreEqual(1, networkDatastore.SegmentCount);
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(machineBlock.BlockInstanceId, out var joined));
            Assert.AreEqual(1, joined.Consumers.Count);
            Assert.AreEqual(1, joined.Generators.Count);

            // 電柱を削除
            // Remove the pole
            worldBlockDatastore.RemoveBlock(Pos(2, 0), BlockRemoveReason.ManualRemove);

            // 機械・発電機は繋がりを失い各自単独セグメント化
            // The machine and generator lose their link and each becomes a standalone segment
            Assert.AreEqual(2, networkDatastore.SegmentCount);
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(machineBlock.BlockInstanceId, out var machineSegment));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(generatorBlock.BlockInstanceId, out var generatorSegment));
            Assert.AreNotSame(machineSegment, generatorSegment);
            Assert.AreEqual(1, machineSegment.Consumers.Count);
            Assert.AreEqual(1, generatorSegment.Generators.Count);

            // 破壊後にtickしてもクラッシュしないこと
            // Ticking after removal must not crash
            Assert.DoesNotThrow(() => GameUpdater.UpdateOneTick());
        }

        // ループ状に繋いだ電柱は1本削除しても連結が保たれ1セグメントのまま
        // A looped pole network keeps its connectivity as a single segment after removing one pole
        [Test]
        public void LoopedSegmentStaysConnectedAfterRemovingOnePole()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole1);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 4本を四角形ループに接続
            // Wire the four poles into a square loop
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(2, 0));
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(2, 2));
            ElectricWireTestUtil.Connect(Pos(2, 2), Pos(0, 2));
            ElectricWireTestUtil.Connect(Pos(0, 2), Pos(0, 0));

            Assert.AreEqual(1, networkDatastore.SegmentCount);

            // ループ上の1本を削除しても残りは別経路で繋がったまま
            // Removing one pole on the loop still leaves the rest connected via the alternate path
            worldBlockDatastore.RemoveBlock(Pos(2, 0), BlockRemoveReason.ManualRemove);
            Assert.AreEqual(1, networkDatastore.SegmentCount);

            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole1.BlockInstanceId, out var segment));
            Assert.AreEqual(3, segment.EnergyTransformers.Count);
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
