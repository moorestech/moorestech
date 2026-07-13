using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // ChangeBlockStateEventPacketのペイロード差分ブロードキャストを検証する
    // Verifies payload-diff broadcasting in ChangeBlockStateEventPacket
    public class ChangeBlockStateEventPacketDedupTest
    {
        [Test]
        // 同一ペイロードで2回ChangeStateを呼んでも、キューに積まれるのは1回のみ
        // Calling ChangeState twice with an identical payload queues only one event
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
            eventProtocolProvider.GetEventBytesList(playerId); // キューをクリア / clear the queue

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
            eventProtocolProvider.GetEventBytesList(playerId); // キューをクリア / clear the queue

            var firstState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } });
            var secondState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 2 } });

            changeBlockStateEventPacket.ChangeState((firstState, blockData));
            changeBlockStateEventPacket.ChangeState((secondState, blockData));

            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(2, events.Count, "ペイロードが変化したら2回とも積まれるべき");
        }

        [Test]
        // 強制送信経路(ForceChangeState)は同一ペイロードでも必ず積まれる
        // The force-broadcast path (ForceChangeState) always queues, even for an identical payload
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
            eventProtocolProvider.GetEventBytesList(playerId); // キューをクリア / clear the queue

            changeBlockStateEventPacket.ForceChangeState((blockState, blockData));
            changeBlockStateEventPacket.ForceChangeState((blockState, blockData));

            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(2, events.Count, "強制送信経路は同一ペイロードでも必ず積まれるべき");
        }
    }
}
