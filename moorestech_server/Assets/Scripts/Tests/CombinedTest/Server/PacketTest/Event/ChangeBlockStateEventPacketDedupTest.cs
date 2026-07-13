using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.InvokeBlockStateEventProtocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // ペイロード差分ブロードキャストを検証
    // Verifies payload-diff broadcasting
    public class ChangeBlockStateEventPacketDedupTest
    {
        [Test]
        // 同一ペイロードは2回呼んでも1回のみ積まれる
        // An identical payload queues only once across two calls
        public void SamePayloadIsBroadcastOnceTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(5, 0, 5);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);
            var blockState = blockData.Block.GetBlockState();

            var playerId = 0;
            // キューをクリア
            // Clear the event queue
            eventProtocolProvider.GetEventBytesList(playerId);

            changeBlockStateEventPacket.ChangeState((blockState, blockData));
            changeBlockStateEventPacket.ChangeState((blockState, blockData));

            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(1, events.Count, "同一ペイロードなら1回のみ積まれるべき");
        }

        [Test]
        // ペイロードが変化した場合は2回とも積まれる
        // Both calls are queued when the payload actually differs
        public void ChangedPayloadIsBroadcastEachTimeTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(6, 0, 6);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);

            var playerId = 0;
            // キューをクリア
            // Clear the event queue
            eventProtocolProvider.GetEventBytesList(playerId);

            var firstState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } });
            var secondState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 2 } });

            changeBlockStateEventPacket.ChangeState((firstState, blockData));
            changeBlockStateEventPacket.ChangeState((secondState, blockData));

            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(2, events.Count, "ペイロードが変化したら2回とも積まれるべき");
        }

        [Test]
        // 強制送信経路は同一ペイロードでも必ず積まれる
        // The force path always queues, even for an identical payload
        public void ForceChangeStateAlwaysBroadcastsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(7, 0, 7);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);
            var blockState = blockData.Block.GetBlockState();

            var playerId = 0;
            // キューをクリア
            // Clear the event queue
            eventProtocolProvider.GetEventBytesList(playerId);

            changeBlockStateEventPacket.ForceChangeState((blockState, blockData));
            changeBlockStateEventPacket.ForceChangeState((blockState, blockData));

            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(2, events.Count, "強制送信経路は同一ペイロードでも必ず積まれるべき");
        }

        [Test]
        // 同一ペイロードでもpull経由は必ず積まれる
        // A pull always queues, even for an identical payload
        public void InvokeBlockStateEventProtocolAlwaysBroadcastsEvenWhenUnchangedTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(8, 0, 8);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);
            var blockState = blockData.Block.GetBlockState();

            // 前回ペイロードとして記録させる
            // Record the payload as the previously broadcast one
            changeBlockStateEventPacket.ChangeState((blockState, blockData));

            var playerId = 0;
            // キューをクリア
            // Clear the event queue
            eventProtocolProvider.GetEventBytesList(playerId);

            // 実パケット経路で発行
            // Fire through the real packet path
            var request = new RequestInvokeBlockStateProtocolMessagePack(pos);
            var payload = MessagePackSerializer.Serialize(request);
            packet.GetPacketResponse(payload, new PacketResponseContext());

            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(1, events.Count, "変化がなくてもpull経路では必ず1件積まれるべき");
        }
    }
}
