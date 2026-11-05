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
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // 状態の束の送信タイミングと掃除を検証
    // Verifies when the block-state bundle (ADR 0091) is sent and how it is cleaned up
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

            // tickで自ら状態を出さないチェストに置き、ダミー状態だけを見る
            // Use a chest, which raises no state of its own during a tick, so only the dummy states are observed
            var firstPos = new Vector3Int(20, 0, 20);
            var secondPos = new Vector3Int(22, 0, 20);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, firstPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, secondPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
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
        // 同じtick内の同じ座標の途中の状態も、発生順に束へ載る
        // Intermediate states of one position within a tick are all bundled in the order they happened
        public void IntermediateStatesOfSamePositionAreKeptInOrderTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(30, 0, 30);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            GameUpdater.UpdateOneTick();
            sink.TakeAll();

            // 1→2→1と変え、最後が最初と同じ中身でも途中の2を挟むので3件とも溜まる
            // Change 1→2→1; the last equals the first, but the 2 in between makes all three queued
            foreach (var dummy in new byte[] { 1, 2, 1 })
            {
                changeBlockStateEventPacket.QueueChangedState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new[] { dummy } }), world.GetOriginPosBlock(pos)));
            }
            GameUpdater.UpdateOneTick();

            var dummies = EventTestUtil.ExtractBlockStates(sink.TakeAll()).Where(s => (Vector3Int)s.Position == pos).Select(s => s.CurrentStateDetail["dummy"][0]).ToList();
            CollectionAssert.AreEqual(new byte[] { 1, 2, 1 }, dummies, "同じ座標の途中の状態も発生順に届くべき");
        }

        [Test]
        // 変化が無いときは束を送らない
        // No bundle is sent when nothing changed
        public void NothingPendingSendsNoEventTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            GameUpdater.UpdateOneTick();
            sink.TakeAll();

            GameUpdater.UpdateOneTick();

            Assert.AreEqual(0, sink.TakeAll().Count(e => e.Tag == ChangeBlockStateEventPacket.EventTag), "溜めた状態が無ければ何も送らないべき");
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
            GameUpdater.UpdateOneTick();
            sink.TakeAll();

            changeBlockStateEventPacket.QueueChangedState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } }), world.GetOriginPosBlock(pos)));
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            var positions = EventTestUtil.ExtractBlockStates(sink.TakeAll()).Select(s => (Vector3Int)s.Position).ToList();
            CollectionAssert.DoesNotContain(positions, pos, "撤去したブロックの状態は捨てられるべき");
        }

        [Test]
        // 手組みした束のバイト列が束の型をシリアライズしたものと一致する
        // The hand-built bundle bytes match a serialization of the bundle type
        public void HandBuiltBundleMatchesSerializedBundleTypeTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var firstPos = new Vector3Int(26, 0, 26);
            var secondPos = new Vector3Int(28, 0, 26);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, firstPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, secondPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            GameUpdater.UpdateOneTick();
            sink.TakeAll();

            changeBlockStateEventPacket.QueueChangedState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 3 } }), world.GetOriginPosBlock(firstPos)));
            changeBlockStateEventPacket.QueueChangedState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 4 } }), world.GetOriginPosBlock(secondPos)));
            GameUpdater.UpdateOneTick();

            // 受信側の型で読めて、中身が溜めた状態と一致する
            // The receiver's type reads it, and the content matches the queued states
            var payload = sink.TakeAll().Single(e => e.Tag == ChangeBlockStateEventPacket.EventTag).Payload;
            var bundle = MessagePackSerializer.Deserialize<BlockStatesTickBundleMessagePack>(payload);
            Assert.AreEqual(2, bundle.States.Length);
            var dummyByPosition = bundle.States.ToDictionary(state => (Vector3Int)state.Position, state => state.CurrentStateDetail["dummy"][0]);
            Assert.AreEqual(3, dummyByPosition[firstPos]);
            Assert.AreEqual(4, dummyByPosition[secondPos]);

            // 束の型を普通にシリアライズしたバイト列と同一である
            // The bytes are identical to an ordinary serialization of the bundle type
            var reserialized = MessagePackSerializer.Serialize(new BlockStatesTickBundleMessagePack(bundle.States));
            CollectionAssert.AreEqual(reserialized, payload, "手組みの束は束の型のシリアライズと同じバイト列であるべき");
        }
    }
}
