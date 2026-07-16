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
using static Server.Protocol.PacketResponse.RequestBlockStateProtocol;

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
            var request = new RequestBlockStateProtocolMessagePack(pos);
            var payload = MessagePackSerializer.Serialize(request);
            packet.GetPacketResponseForTest(payload, new PacketResponseContext());

            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(1, events.Count, "変化がなくてもpull経路では必ず1件積まれるべき");
        }

        [Test]
        // Destroy中発火によるstale再登録を経ても再設置後の初回送信は必ず積まれる
        // The first send after re-placement always queues, even through a stale re-registration during Destroy()
        public void RePlacedBlockBroadcastsAfterStaleReRegistrationTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            // 再設置後も同一に見える状態（既定状態が偶然一致するケースを模す固定ペイロード）
            // A fixed state that looks identical after re-placement, mirroring a coincidentally-matching default state
            var sharedState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } });

            var pos = new Vector3Int(9, 0, 9);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var firstBlockData = world.GetOriginPosBlock(pos);

            // (1) 設置＋ChangeStateでペイロード記録
            // (1) Place, then ChangeState records the payload
            changeBlockStateEventPacket.ChangeState((sharedState, firstBlockData));

            // (2) 削除（削除イベント発火→Block.Destroy()の順）
            // (2) Remove (fires remove event, then Block.Destroy())
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);

            // (3) Destroy中発火を模し、削除直後に同一(state, blockData)でChangeStateを呼びstale再登録を再現
            // (3) Simulate a Destroy()-time event: re-register the stale entry right after removal
            changeBlockStateEventPacket.ChangeState((sharedState, firstBlockData));

            var playerId = 0;
            // (4) キューをクリア
            // (4) Clear the event queue
            eventProtocolProvider.GetEventBytesList(playerId);

            // (5) 同座標に再設置。設置イベント自体のブロードキャストは対象外なので直後に取り除く
            // (5) Re-place at the same position; drain the unrelated placement broadcast right after
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var secondBlockData = world.GetOriginPosBlock(pos);
            eventProtocolProvider.GetEventBytesList(playerId);

            // (6) 再設置ブロックのstateでChangeState（既定状態がstale値と一致するケースを模す）
            // (6) ChangeState with the re-placed block's state (mirrors a default state matching the stale value)
            changeBlockStateEventPacket.ChangeState((sharedState, secondBlockData));

            // (7) staleエントリの掃除により必ず1件積まれるべき
            // (7) Must queue exactly once thanks to the stale-entry cleanup
            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(1, events.Count, "再設置後の初回送信は必ず積まれるべき");
        }
    }
}
