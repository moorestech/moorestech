using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    ///     発電機や機械が削除されたときに、セグメントから正しく削除されるかをテストする
    /// </summary>
    public class DisconnectMachineFromElectricSegmentTest
    {
        [Test]
        public void RemoveGeneratorToDisconnectFromSegment()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            /*設置する電柱、発電機の場所
             * G □ P □ G
             */

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, out _, System.Array.Empty<BlockCreateParam>());

            //発電機の設定
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(0, 0), BlockDirection.North, out var generator1Block, System.Array.Empty<BlockCreateParam>());
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(4, 0), BlockDirection.North, out var generator2Block, System.Array.Empty<BlockCreateParam>());

            var generator1InstanceId = generator1Block.BlockInstanceId;
            var generator2InstanceId = generator2Block.BlockInstanceId;

            IWorldEnergySegmentDatastore<EnergySegment> worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();

            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //セグメントに2つの発電機が登録されていることを確認
            var segment = worldElectricSegment.GetEnergySegment(0);
            Assert.AreEqual(2, segment.Generators.Count);
            Assert.IsTrue(segment.Generators.ContainsKey(generator1InstanceId));
            Assert.IsTrue(segment.Generators.ContainsKey(generator2InstanceId));

            //左の発電機を削除
            worldBlockDatastore.RemoveBlock(Pos(0, 0));

            //セグメントの数は変わらないことを確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //左の発電機がセグメントから削除されていることを確認
            segment = worldElectricSegment.GetEnergySegment(0);
            Assert.AreEqual(1, segment.Generators.Count);
            Assert.IsFalse(segment.Generators.ContainsKey(generator1InstanceId));
            Assert.IsTrue(segment.Generators.ContainsKey(generator2InstanceId));

            //右の発電機も削除
            worldBlockDatastore.RemoveBlock(Pos(4, 0));

            //セグメントの数は変わらないことを確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //両方の発電機がセグメントから削除されていることを確認
            segment = worldElectricSegment.GetEnergySegment(0);
            Assert.AreEqual(0, segment.Generators.Count);
            Assert.IsFalse(segment.Generators.ContainsKey(generator1InstanceId));
            Assert.IsFalse(segment.Generators.ContainsKey(generator2InstanceId));
        }

        [Test]
        public void RemoveMachineToDisconnectFromSegment()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            /*設置する電柱、機械の場所
             * M □ P □ M
             */

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, out _, System.Array.Empty<BlockCreateParam>());

            //機械の設定
            worldBlockDatastore.TryAddBlock(MachineId, Pos(0, 0), BlockDirection.North, out var machine1Block, System.Array.Empty<BlockCreateParam>());
            worldBlockDatastore.TryAddBlock(MachineId, Pos(4, 0), BlockDirection.North, out var machine2Block, System.Array.Empty<BlockCreateParam>());

            var machine1InstanceId = machine1Block.BlockInstanceId;
            var machine2InstanceId = machine2Block.BlockInstanceId;

            IWorldEnergySegmentDatastore<EnergySegment> worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();

            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //セグメントに2つの機械が登録されていることを確認
            var segment = worldElectricSegment.GetEnergySegment(0);
            Assert.AreEqual(2, segment.Consumers.Count);
            Assert.IsTrue(segment.Consumers.ContainsKey(machine1InstanceId));
            Assert.IsTrue(segment.Consumers.ContainsKey(machine2InstanceId));

            //左の機械を削除
            worldBlockDatastore.RemoveBlock(Pos(0, 0));

            //セグメントの数は変わらないことを確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //左の機械がセグメントから削除されていることを確認
            segment = worldElectricSegment.GetEnergySegment(0);
            Assert.AreEqual(1, segment.Consumers.Count);
            Assert.IsFalse(segment.Consumers.ContainsKey(machine1InstanceId));
            Assert.IsTrue(segment.Consumers.ContainsKey(machine2InstanceId));

            //右の機械も削除
            worldBlockDatastore.RemoveBlock(Pos(4, 0));

            //セグメントの数は変わらないことを確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //両方の機械がセグメントから削除されていることを確認
            segment = worldElectricSegment.GetEnergySegment(0);
            Assert.AreEqual(0, segment.Consumers.Count);
            Assert.IsFalse(segment.Consumers.ContainsKey(machine1InstanceId));
            Assert.IsFalse(segment.Consumers.ContainsKey(machine2InstanceId));
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
