using System;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using MessagePack;
using NUnit.Framework;
using Server.Util.MessagePack.BeltSegment;
using UnityEngine;
using static Client.Tests.BeltSegment.Network.BeltNetworkFixture;
namespace Client.Tests.BeltSegment.Network
{
    public sealed class BeltWireRoundTripTest
    {
        [SetUp] public void Setup() => LoadMaster();
        [Test]
        public void SnapshotAndHundredFramesRoundTripFullWidthClockAndGpuMetadata()
        {
            var initial = Single((ulong)uint.MaxValue + 99, 17, 2);
            var decoded = RoundTrip(initial); var world = World(); world.ReceiveSnapshot(decoded);
            var server = new BeltReplaySimulation(initial.Simulation);
            for (int tick = 0; tick < 100; tick++)
            {
                var frame = Frame(initial, EmptyTick());
                var bytes = MessagePackSerializer.Serialize(new BeltWorldFrameMessagePack(frame));
                var wire = BeltWireCodec.Decode(MessagePackSerializer.Deserialize<BeltWorldFrameMessagePack>(bytes));
                world.ReceiveFrame(wire); server.ApplyTick(frame.Replay, false);
                Assert.AreEqual(server.ComputeStateHash(), new BeltReplaySimulation(world.CaptureCpuState()).ComputeStateHash());
                GpuBeltReplayReadback.AssertMatches(server.CaptureSnapshot(), world.Simulation, tick);
                initial = new(frame.Position, 2, server.CaptureSnapshot(), initial.Routes);
            }
            Assert.AreEqual((ulong)uint.MaxValue + 199, world.Position.Tick); world.Simulation.Dispose();
        }
        [Test]
        public void PackingOwnsDetachedPositionValues()
        {
            var snapshot = Single(0, 0, 1); var item = snapshot.Simulation.Segments[0].Items[0].Item;
            item.Position = new(new(3, 4, 5), BeltEntryDirection.FromLeftAbove, 123);
            snapshot.Simulation.Segments[0].Items[0] = new(item, 240);
            var wire = new BeltWorldSnapshotMessagePack(snapshot);
            item.Position.MoveTo(new(7, 8, 9), BeltEntryDirection.FromBack, 12);
            var decoded = BeltWireCodec.Decode(wire).Simulation.Segments[0].Items[0].Item.Position;
            Assert.AreEqual(new BeltCell(3, 4, 5), decoded.CurrentCell); Assert.AreEqual(123, decoded.Progress);
        }
        [TestCase("missing")][TestCase("capacity")][TestCase("route")][TestCase("entry")]
        [TestCase("buffer")][TestCase("item")][TestCase("spacing")][TestCase("direction")]
        [TestCase("id")][TestCase("duplicate")][TestCase("priority")][TestCase("truncated")]
        public void InvalidSnapshotIsRejectedBeforeStateConstruction(string invalid)
        {
            var wire = new BeltWorldSnapshotMessagePack(Single(0, 0, 1));
            switch (invalid)
            {
                case "missing": wire.Segments = null; break;
                case "capacity": wire.Segments[0].Capacity = int.MaxValue; break;
                case "route": wire.Routes[0].Cells = Array.Empty<BeltRouteCellMessagePack>(); break;
                case "entry": wire.Routes[0].EntryCells = new BeltRouteCellMessagePack[3]; break;
                case "buffer": wire.Segments[0].BufferedItem = wire.Segments[0].Items[0].Item; break;
                case "item": wire.Segments[0].Items[0].Item.Kind = new Core.Master.ItemId(-1); break;
                case "spacing": wire.Segments[0].Items[0].Distance = -1; break;
                case "direction": wire.Inputs[0].Direction = BeltDirection.None; break;
                case "id": wire.Inputs[0].Target = 99; break;
                case "duplicate": wire.Inputs = new[] { wire.Inputs[0], wire.Inputs[0] }; break;
                case "priority": wire.Segments[0].PriorityIndex = 1; break;
                case "truncated": wire.Routes[0].EntryCells[0].Complete = false; break;
            }
            Assert.Throws<ArgumentException>(() => BeltWireCodec.Decode(wire));
        }
        [Test]
        public void InvalidTickRejectsDuplicateIdsAndNonpositiveInsertion()
        {
            var wire = new BeltWorldFrameMessagePack(Frame(Single(0, 0, 1), EmptyTick()));
            wire.Replay.ReadyInputs = new[] { 0, 0 };
            Assert.Throws<ArgumentException>(() => BeltWireCodec.Decode(wire));
            wire.Replay.ReadyInputs = Array.Empty<int>();
            var source = Single(0, 0, 1);
            wire = new BeltWorldFrameMessagePack(Frame(source, new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(),
                Array.Empty<int>(), Array.Empty<int>(), new[] { new BeltReplayInsertion(0, 0, source.Simulation.Segments[0].Items[0].Item) })));
            Assert.Throws<ArgumentException>(() => BeltWireCodec.Decode(wire));
        }
        [TestCase(false)][TestCase(true)]
        public void ReplayAndPackingMeasurementRecordsActualBytesAndAllocations(bool withEvents)
        {
            var snapshot = Single(0, 0, 1); var cpu = new BeltReplaySimulation(snapshot.Simulation);
            var tick = withEvents ? new BeltReplayTick(new[] { new BeltReplaySpeedChange(0, 16) }, new[] { 0 },
                Array.Empty<int>(), Array.Empty<BeltReplayInsertion>()) : EmptyTick();
            var pack = new GpuBeltTickUpload(1, 0, 1);
            cpu.ApplyTick(tick, false); pack.Prepare(tick);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++) { cpu.ApplyTick(tick, false); pack.Prepare(tick); }
            timer.Stop(); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            int snapshotBytes = MessagePackSerializer.Serialize(new BeltWorldSnapshotMessagePack(snapshot)).Length;
            int frameBytes = MessagePackSerializer.Serialize(new BeltWorldFrameMessagePack(Frame(snapshot, tick))).Length;
            Debug.Log($"replay+GPU packing: segments=1 items=1 withEvents={withEvents} iterations=10000 elapsedMs={timer.Elapsed.TotalMilliseconds:F3} allocatedBytes={allocated}; snapshotBytes={snapshotBytes}; frameBytes={frameBytes}; gpuEventBytes={pack.Prepare(tick) * 16}");
            Assert.AreEqual(withEvents ? 2 : 0, pack.Prepare(tick));
        }
        private static BeltWorldSnapshot RoundTrip(BeltWorldSnapshot snapshot) => BeltWireCodec.Decode(
            MessagePackSerializer.Deserialize<BeltWorldSnapshotMessagePack>(MessagePackSerializer.Serialize(new BeltWorldSnapshotMessagePack(snapshot))));
    }
}
