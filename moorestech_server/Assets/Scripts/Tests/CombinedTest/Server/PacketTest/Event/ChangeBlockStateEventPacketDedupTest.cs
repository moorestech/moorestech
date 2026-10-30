using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
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
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(5, 0, 5);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);
            var blockState = blockData.Block.GetBlockState();

            var playerId = 1;
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, playerId);
            // キューをクリア
            // Clear the event queue
            sink.TakeAll();

            changeBlockStateEventPacket.QueueChangedState((blockState, blockData));
            changeBlockStateEventPacket.QueueChangedState((blockState, blockData));
            GameUpdater.UpdateOneTick();

            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(1, states.Count, "同一ペイロードなら1件のみ載るべき");
        }

        [Test]
        // 送信を挟んで変化したら各tickの束に載る
        // When the payload changes across a broadcast, each tick's bundle carries it
        public void ChangedPayloadAcrossTicksIsBroadcastEachTimeTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            // tickで自ら状態を出さないチェストに置き、ダミー状態だけを見る
            // Use a chest, which raises no state of its own during a tick, so only the dummy states are observed
            var pos = new Vector3Int(6, 0, 6);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);

            var playerId = 1;
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, playerId);
            // キューをクリア
            // Clear the event queue
            sink.TakeAll();

            var firstState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } });
            var secondState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 2 } });

            changeBlockStateEventPacket.QueueChangedState((firstState, blockData));
            GameUpdater.UpdateOneTick();
            changeBlockStateEventPacket.QueueChangedState((secondState, blockData));
            GameUpdater.UpdateOneTick();

            var events = sink.TakeAll().Where(e => e.Tag == ChangeBlockStateEventPacket.EventTag).ToList();
            Assert.AreEqual(2, events.Count, "tickごとに束が1件ずつ送られるべき");
            var states = EventTestUtil.ExtractBlockStates(events);
            Assert.AreEqual(2, states.Count, "両方の状態が載るべき");
            Assert.AreEqual(1, states[0].CurrentStateDetail["dummy"][0], "1tick目は先の状態が載るべき");
            Assert.AreEqual(2, states[1].CurrentStateDetail["dummy"][0], "2tick目は後の状態が載るべき");
        }

        [Test]
        // 強制送信経路は同一ペイロードでも必ず積まれる
        // The force path always queues, even for an identical payload
        public void QueueStateForInitialPullAlwaysQueuesTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(7, 0, 7);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);
            var blockState = blockData.Block.GetBlockState();

            var playerId = 1;
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, playerId);
            // キューをクリア
            // Clear the event queue
            sink.TakeAll();

            changeBlockStateEventPacket.QueueStateForInitialPull((blockState, blockData));
            GameUpdater.UpdateOneTick();
            changeBlockStateEventPacket.QueueStateForInitialPull((blockState, blockData));
            GameUpdater.UpdateOneTick();

            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(2, states.Count, "強制送信経路は同一ペイロードでも毎回載るべき");
        }

        [Test]
        // 同一ペイロードでもpull経由は必ず積まれる
        // A pull always queues, even for an identical payload
        public void InvokeBlockStateEventProtocolAlwaysBroadcastsEvenWhenUnchangedTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(8, 0, 8);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var blockData = world.GetOriginPosBlock(pos);
            var blockState = blockData.Block.GetBlockState();

            // 前回ペイロードとして記録させる
            // Record the payload as the previously broadcast one
            changeBlockStateEventPacket.QueueChangedState((blockState, blockData));
            GameUpdater.UpdateOneTick();

            var playerId = 1;
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, playerId);
            // キューをクリア
            // Clear the event queue
            sink.TakeAll();

            // 実パケット経路で発行
            // Fire through the real packet path
            var request = new RequestBlockStateProtocolMessagePack(pos);
            var payload = MessagePackSerializer.Serialize(request);
            packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(playerId));
            GameUpdater.UpdateOneTick();

            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(1, states.Count, "変化がなくてもpull経路では必ず1件載るべき");
        }

        [Test]
        // Destroy中発火によるstale再登録を経ても再設置後の初回送信は必ず積まれる
        // The first send after re-placement always queues, even through a stale re-registration during Destroy()
        public void RePlacedBlockBroadcastsAfterStaleReRegistrationTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            // 再設置後も同一に見える状態（既定状態が偶然一致するケースを模す固定ペイロード）
            // A fixed state that looks identical after re-placement, mirroring a coincidentally-matching default state
            var sharedState = new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } });

            // tickで自ら状態を出さないチェストに置き、ダミー状態だけを見る
            // Use a chest, which raises no state of its own during a tick, so only the dummy states are observed
            var pos = new Vector3Int(9, 0, 9);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var firstBlockData = world.GetOriginPosBlock(pos);

            // (1) 設置し状態を記録
            // (1) Place, then QueueChangedState records the payload
            changeBlockStateEventPacket.QueueChangedState((sharedState, firstBlockData));

            // (2) 削除（削除イベント発火→Block.Destroy()の順）
            // (2) Remove (fires remove event, then Block.Destroy())
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);

            // (3) 削除直後の再登録（stale）を再現
            // (3) Simulate a Destroy()-time event: re-register the stale entry right after removal
            changeBlockStateEventPacket.QueueChangedState((sharedState, firstBlockData));

            var playerId = 1;
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, playerId);
            // (4) キューをクリア
            // (4) Clear the event queue
            sink.TakeAll();

            // (5) 同座標に再設置。設置イベント自体のブロードキャストは対象外なので直後に取り除く
            // (5) Re-place at the same position; drain the unrelated placement broadcast right after
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var secondBlockData = world.GetOriginPosBlock(pos);
            sink.TakeAll();

            // (6) 再設置ブロックのstateでQueueChangedState（既定状態がstale値と一致するケースを模す）
            // (6) QueueChangedState with the re-placed block's state (mirrors a default state matching the stale value)
            changeBlockStateEventPacket.QueueChangedState((sharedState, secondBlockData));
            GameUpdater.UpdateOneTick();

            // (7) staleエントリの掃除により必ず1件積まれるべき
            // (7) Must queue exactly once thanks to the stale-entry cleanup
            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(1, states.Count, "再設置後の初回送信は必ず載るべき");
        }
    }
}
