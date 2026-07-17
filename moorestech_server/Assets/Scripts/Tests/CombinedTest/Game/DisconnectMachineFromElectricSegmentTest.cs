using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    ///     ワイヤー接続された発電機・機械を削除したときに、セグメントから役割が正しく外れるかを検証する
    ///     Verify that removing a wired generator or machine correctly detaches its role from the segment
    /// </summary>
    public class DisconnectMachineFromElectricSegmentTest
    {
        // 2つの発電機を順に削除し残る発電機が減るか確認
        // Remove two generators wired to a pole one by one and confirm the surviving generators shrink correctly
        [Test]
        public void RemoveGeneratorDetachesFromSegment()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkLookup>();

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleBlock);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator1Block);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(4, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator2Block);
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(0, 0));
            GameUpdater.UpdateOneTick();
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(4, 0));
            GameUpdater.UpdateOneTick();

            // 1セグメントに2つの発電機が登録されている
            // One segment holds both generators
            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(poleBlock.BlockInstanceId, out var segment));
            Assert.AreEqual(2, GetGenerators(segment).Count);
            Assert.IsTrue(GetGenerators(segment).ContainsKey(generator1Block.BlockInstanceId));
            Assert.IsTrue(GetGenerators(segment).ContainsKey(generator2Block.BlockInstanceId));

            // 左の発電機を削除
            // Remove the left generator
            worldBlockDatastore.RemoveBlock(Pos(0, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            // 電柱と右発電機は繋がったままなのでセグメント数は1
            // The pole and right generator remain wired, so the segment count stays 1
            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(poleBlock.BlockInstanceId, out segment));
            Assert.AreEqual(1, GetGenerators(segment).Count);
            Assert.IsFalse(GetGenerators(segment).ContainsKey(generator1Block.BlockInstanceId));
            Assert.IsTrue(GetGenerators(segment).ContainsKey(generator2Block.BlockInstanceId));

            // 右の発電機も削除
            // Remove the right generator too
            worldBlockDatastore.RemoveBlock(Pos(4, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            // 電柱単独のセグメントが残り、発電機は0
            // Only the pole-only segment remains with zero generators
            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(poleBlock.BlockInstanceId, out segment));
            Assert.AreEqual(0, GetGenerators(segment).Count);
        }

        // 2つの機械を順に削除し残る消費者が減るか確認
        // Remove two machines wired to a pole one by one and confirm the surviving consumers shrink correctly
        [Test]
        public void RemoveMachineDetachesFromSegment()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkLookup>();

            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleBlock);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine1Block);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(4, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine2Block);
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(0, 0));
            GameUpdater.UpdateOneTick();
            ElectricWireTestUtil.Connect(Pos(2, 0), Pos(4, 0));
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(poleBlock.BlockInstanceId, out var segment));
            Assert.AreEqual(2, GetConsumers(segment).Count);
            Assert.IsTrue(GetConsumers(segment).ContainsKey(machine1Block.BlockInstanceId));
            Assert.IsTrue(GetConsumers(segment).ContainsKey(machine2Block.BlockInstanceId));

            // 左の機械を削除
            // Remove the left machine
            worldBlockDatastore.RemoveBlock(Pos(0, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(poleBlock.BlockInstanceId, out segment));
            Assert.AreEqual(1, GetConsumers(segment).Count);
            Assert.IsFalse(GetConsumers(segment).ContainsKey(machine1Block.BlockInstanceId));
            Assert.IsTrue(GetConsumers(segment).ContainsKey(machine2Block.BlockInstanceId));

            // 右の機械も削除
            // Remove the right machine too
            worldBlockDatastore.RemoveBlock(Pos(4, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(poleBlock.BlockInstanceId, out segment));
            Assert.AreEqual(0, GetConsumers(segment).Count);
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
