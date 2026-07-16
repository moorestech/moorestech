using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetElectricNetworkInfoProtocolTest
    {
        [Test]
        public void GetElectricNetworkInfo_ReturnsSameAggregateForEveryMemberBlock()
        {
            // 電柱・発電機・機械を1セグメントに構築し、いずれのブロックIDからも同一の集約値が返るか確認
            // Build one segment from a pole, generator, and machine, then assert every member block reports the same aggregate
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, Pos(0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            // 電柱に発電機と機械をワイヤー接続して1セグメントにまとめる
            // Wire the generator and machine to the pole to form one segment
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(0, 2));
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(2, 0));

            // トポロジ反映と統計確定はtick先頭で行われるため1tick進める
            // Advance one tick so the topology flush and statistics settlement run
            GameUpdater.UpdateOneTick();

            var segmentDatastore = serviceProvider.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(segmentDatastore.TryGetEnergySegment(pole.BlockInstanceId, out var segment));

            var expected = segment.Statistics;
            // 無限発電機を使うため発電量は正、機械が消費者として1台登録される
            // The infinity generator yields positive generation and the machine registers as one consumer
            Assert.Greater(expected.TotalGeneratePower, 0f);
            Assert.AreEqual(1, expected.ConsumerCount);

            // 電柱・発電機・機械のいずれのIDでも同じ集約値が返ることを確認
            // Confirm the pole, generator, and machine IDs all report the same aggregate
            foreach (var id in new[] { pole.BlockInstanceId, generator.BlockInstanceId, machine.BlockInstanceId })
            {
                var info = InvokeGetElectricNetworkInfo(packet, id).Info;
                Assert.IsNotNull(info);
                Assert.AreEqual(expected.TotalGeneratePower, info.TotalGeneratePower);
                Assert.AreEqual(expected.TotalRequiredPower, info.TotalRequiredPower);
                Assert.AreEqual(expected.PowerRate, info.PowerRate);
                Assert.AreEqual(expected.ConsumerCount, info.ConsumerCount);
            }
        }

        [Test]
        public void GetElectricNetworkInfo_NoConsumer_ReportsNoDemandWithoutDivisionError()
        {
            // 消費者ゼロのセグメントで、ゼロ除算なく需要なし(供給率1.0/要求0/消費者0)が返るか確認
            // With a consumer-less segment, assert no-demand (rate 1.0 / required 0 / consumer 0) without a division error
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, Pos(0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 発電機のみをワイヤー接続し、消費者ゼロのセグメントを作る
            // Wire only the generator to build a consumer-less segment
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(0, 2));

            // トポロジ反映と統計確定のため1tick進める
            // Advance one tick for the topology flush and statistics settlement
            GameUpdater.UpdateOneTick();

            var info = InvokeGetElectricNetworkInfo(packet, pole.BlockInstanceId).Info;

            Assert.IsNotNull(info);
            Assert.AreEqual(0, info.ConsumerCount);
            Assert.AreEqual(0f, info.TotalRequiredPower);
            Assert.AreEqual(1f, info.PowerRate);
        }

        [Test]
        public void GetElectricNetworkInfo_UnknownBlockInstanceId_ReturnsNull()
        {
            // どの電力セグメントにも属さないブロックIDで Info=null が返ることを確認
            // Confirm Info=null is returned for a block that belongs to no energy segment
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var response = InvokeGetElectricNetworkInfo(packet, new BlockInstanceId(int.MinValue + 1));

            Assert.IsNull(response.Info);
        }

        private static GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack InvokeGetElectricNetworkInfo(PacketResponseCreator packet, BlockInstanceId id)
        {
            var request = new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(id);
            var responseBytes = packet.GetPacketResponseForTest(MessagePackSerializer.Serialize(request), new PacketResponseContext());
            return MessagePackSerializer.Deserialize<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack>(responseBytes[0]);
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
