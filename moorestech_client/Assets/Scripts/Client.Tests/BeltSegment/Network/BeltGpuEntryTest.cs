using System;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using NUnit.Framework;
using Server.Util.MessagePack.BeltSegment;
using Tests.Module.TestMod;
using static Client.Tests.BeltSegment.Network.BeltNetworkFixture;
namespace Client.Tests.BeltSegment.Network
{
    public sealed class BeltGpuEntryTest
    {
        [SetUp] public void Setup() => LoadMaster();
        [Test]
        public void SelectedMergeEntrySurvivesRestoredRunningSnapshot()
        {
            var snapshot = new BeltReplaySnapshot(new[] { BeltReplaySegmentState.Merge(16, 1, Array.Empty<BeltItemState>(), null) },
                Array.Empty<BeltReplayLink>(), new[] { new BeltReplayInput(0, BeltDirection.Back), new BeltReplayInput(0, BeltDirection.Left) },
                Array.Empty<BeltReplayOutput>());
            var cpu = new BeltReplaySimulation(snapshot);
            var item = new BeltItem { Guid = Guid.NewGuid(), ItemId = ForUnitTestItemId.ItemId1.AsPrimitive() };
            var tick = new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), new[] { 0, 1 }, Array.Empty<int>(), new[] { new BeltReplayInsertion(1, 16, item) });
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                GpuBeltReplayTest.Apply(cpu, gpu, tick);
                Assert.AreEqual(BeltDirection.Left, cpu.CaptureSnapshot().Segments[0].Items[0].Item.AcceptedInput);
            }
            using var restored = new GpuBeltSimulation(cpu.CaptureSnapshot(), GpuBeltReplayTest.Shader());
            GpuBeltReplayReadback.AssertMatches(cpu.CaptureSnapshot(), restored, 1);
        }
        [TestCase(2)][TestCase(5)]
        public void RetainedPriorityWrapsAfterThreeOutputsShrinkToTwo(int retainedPriority)
        {
            var empty = Array.Empty<BeltItemState>();
            var item = new BeltItem { Guid = Guid.NewGuid(), ItemId = ForUnitTestItemId.ItemId1.AsPrimitive(), AcceptedInput = BeltDirection.Back };
            var snapshot = new BeltReplaySnapshot(new[] { BeltReplaySegmentState.Branch(1, 16, retainedPriority, empty, item),
                BeltReplaySegmentState.Merge(16, 0, empty, null) }, new[] { new BeltReplayLink(0, 1, BeltDirection.Front) },
                Array.Empty<BeltReplayInput>(), new[] { new BeltReplayOutput(0, BeltDirection.Right) });
            var routes = new[] { new BeltRoute(new[] { new BeltRouteCell(new(0, 0, 0), BeltEntryDirection.FromBack, 0, 0) }, new BeltRouteCell[4]),
                new BeltRoute(new[] { new BeltRouteCell(new(0, 1, 0), BeltEntryDirection.FromBack, 0, 0) }, new BeltRouteCell[4]) };
            var world = BeltWireCodec.Decode(new BeltWorldSnapshotMessagePack(new(new(0, 0), 1, snapshot, routes)));
            Assert.AreEqual(retainedPriority, world.Simulation.Segments[0].PriorityIndex);
            var cpu = new BeltReplaySimulation(world.Simulation);
            using var gpu = new GpuBeltSimulation(world.Simulation, GpuBeltReplayTest.Shader());
            GpuBeltReplayReadback.AssertMatches(cpu.CaptureSnapshot(), gpu, 0);
            GpuBeltReplayTest.Apply(cpu, gpu, EmptyTick());
            Assert.AreEqual(retainedPriority % 2 == 0 ? 1 : 0, cpu.CaptureSnapshot().Segments[1].Items.Length);
        }
    }
}
