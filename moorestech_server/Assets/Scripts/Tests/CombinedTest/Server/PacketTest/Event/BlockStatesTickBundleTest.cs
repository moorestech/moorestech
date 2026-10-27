using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // 状態の束の送信タイミングと掃除を検証
    // Verifies when the block-state bundle (ADR 0088) is sent and how it is cleaned up
    public class BlockStatesTickBundleTest
    {
        [Test]
        // tickを進めると溜めた状態が1イベントで届く
        // Advancing a tick delivers the states pending in that tick as one event
        public void TickEndBroadcastsPendingStatesAsOneEventTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var firstPos = new Vector3Int(20, 0, 20);
            var secondPos = new Vector3Int(22, 0, 20);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, firstPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, secondPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            GameUpdater.UpdateOneTick();
            sink.TakeAll();

            // 2ブロックへ同じtick内に状態を溜める
            // Queue states for two blocks within the same tick
            changeBlockStateEventPacket.QueueChangedState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } }), world.GetOriginPosBlock(firstPos)));
            changeBlockStateEventPacket.QueueChangedState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } }), world.GetOriginPosBlock(secondPos)));
            Assert.AreEqual(0, sink.Events.Count(e => e.Tag == ChangeBlockStateEventPacket.EventTag), "tick末尾まで送られないべき");

            GameUpdater.UpdateOneTick();

            var events = sink.TakeAll().Where(e => e.Tag == ChangeBlockStateEventPacket.EventTag).ToList();
            Assert.AreEqual(1, events.Count, "1tickの状態は1イベントにまとまるべき");
            var positions = EventTestUtil.ExtractBlockStates(events).Select(s => (Vector3Int)s.Position).ToList();
            CollectionAssert.Contains(positions, firstPos);
            CollectionAssert.Contains(positions, secondPos);
        }

        [Test]
        // 変化が無いときは束を送らない
        // No bundle is sent when nothing changed
        public void NothingPendingSendsNoEventTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            changeBlockStateEventPacket.BroadcastPendingStates();
            sink.TakeAll();

            changeBlockStateEventPacket.BroadcastPendingStates();

            Assert.AreEqual(0, sink.TakeAll().Count, "溜めた状態が無ければ何も送らないべき");
        }

        [Test]
        // 溜めた後に撤去したブロックの状態は束に載らない
        // A block removed after its state was queued is left out of the bundle
        public void RemovedBlockStateIsDroppedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(24, 0, 24);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            changeBlockStateEventPacket.BroadcastPendingStates();
            sink.TakeAll();

            changeBlockStateEventPacket.QueueChangedState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } }), world.GetOriginPosBlock(pos)));
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            changeBlockStateEventPacket.BroadcastPendingStates();

            var positions = EventTestUtil.ExtractBlockStates(sink.TakeAll()).Select(s => (Vector3Int)s.Position).ToList();
            CollectionAssert.DoesNotContain(positions, pos, "撤去したブロックの状態は捨てられるべき");
        }
    }
}
