using System;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
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
    public class GetGearNetworkInfoProtocolTest
    {
        [Test]
        public void GetGearNetworkInfo_ReturnsCurrentNetworkAggregate()
        {
            // 歯車ネットワークを構築し、1 tick 進めた直後にプロトコルを叩いて集約値が一致するか確認
            // Build a gear network, advance one tick, then invoke the protocol and assert aggregate values match
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaft);

            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            var expected = gearNetwork.CurrentGearNetworkInfo;

            // ジェネレーター・シャフト双方から取得したレスポンスが同一のネットワーク集約値を返すことを確認
            // Both generator and shaft should report the same aggregate network info
            var generatorResponse = InvokeGetGearNetworkInfo(packet, generator.BlockInstanceId);
            var shaftResponse = InvokeGetGearNetworkInfo(packet, shaft.BlockInstanceId);

            Assert.IsNotNull(generatorResponse.Info);
            Assert.IsNotNull(shaftResponse.Info);
            Assert.AreEqual(expected.TotalRequiredGearPower, generatorResponse.Info.TotalRequiredGearPower);
            Assert.AreEqual(expected.TotalGenerateGearPower, generatorResponse.Info.TotalGenerateGearPower);
            Assert.AreEqual(expected.StopReason, generatorResponse.Info.StopReason);
            Assert.AreEqual(generatorResponse.Info.TotalRequiredGearPower, shaftResponse.Info.TotalRequiredGearPower);
            Assert.AreEqual(generatorResponse.Info.TotalGenerateGearPower, shaftResponse.Info.TotalGenerateGearPower);
            Assert.AreEqual(generatorResponse.Info.StopReason, shaftResponse.Info.StopReason);
        }

        [Test]
        public void GetGearNetworkInfo_UnknownBlockInstanceId_ReturnsNull()
        {
            // 存在しないブロックIDに対して Info=null が返ることを確認
            // Confirm Info=null is returned for a non-existent BlockInstanceId
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var response = InvokeGetGearNetworkInfo(packet, new BlockInstanceId(int.MinValue + 1));

            Assert.IsNull(response.Info);
        }

        private static GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack InvokeGetGearNetworkInfo(PacketResponseCreator packet, BlockInstanceId id)
        {
            var request = new GetGearNetworkInfoProtocol.RequestGetGearNetworkInfoMessagePack(id);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request));
            return MessagePackSerializer.Deserialize<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack>(responseBytes[0]);
        }
    }
}
