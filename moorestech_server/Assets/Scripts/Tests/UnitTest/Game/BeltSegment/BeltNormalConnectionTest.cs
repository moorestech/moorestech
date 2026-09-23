using System;
using System.Linq;
using Game.BeltSegment;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltNormalConnectionTest
    {
        [Test]
        public void IncomingNormalItemAdvancesExactlyOnce([Values] bool parallel, [Values] bool reverse)
        {
            var source = Normal(1, 64);
            var target = Normal(1, 64);
            source.ConnectTo(target, BeltDirection.Front);
            source.RestoreItems(new[] { State(1, 32) });
            Simulation(reverse, source, target).Tick(parallel);
            Assert.That(source.Count, Is.Zero);
            AssertItems(target, new[] { 1 }, new[] { 224 });
            Assert.That(source.Output, Is.SameAs(target));
        }

        [Test]
        public void EmptySimulationAndSelfConnectionStayEmpty([Values] bool parallel)
        {
            new BeltSimulation(Array.Empty<BeltConveyorSegment>()).Tick(parallel);
            var segment = Normal(1, 64);
            segment.ConnectTo(segment, BeltDirection.Front);
            new BeltSimulation(new[] { segment }).Tick(parallel);
            Assert.That(segment.Count, Is.Zero);
        }

        [TestCase(0, 32, false)]
        [TestCase(64, 992, false)]
        [TestCase(128, 928, false)]
        [TestCase(0, 32, true)]
        [TestCase(64, 992, true)]
        [TestCase(128, 928, true)]
        public void SelfConnectionUsesOnlyRemainingMovement(int speed, int expected, bool parallel)
        {
            var segment = Normal(4, speed);
            segment.ConnectTo(segment, BeltDirection.Front);
            segment.RestoreItems(new[] { State(1, 32) });
            new BeltSimulation(new[] { segment }).Tick(parallel);
            AssertItems(segment, new[] { 1 }, new[] { expected });
        }

        [Test]
        public void FullSelfConnectionStops([Values] bool parallel, [Values(1, 2)] int capacity)
        {
            var segment = Normal(capacity, 64);
            segment.ConnectTo(segment, BeltDirection.Front);
            segment.RestoreItems(capacity == 1 ? new[] { State(1, 32) } : new[] { State(1, 0), State(2, 256) });
            var simulation = new BeltSimulation(new[] { segment });
            for (int tick = 0; tick < 2; tick++)
            {
                simulation.Tick(parallel);
                AssertItems(segment, Enumerable.Range(1, capacity).ToArray(),
                    Enumerable.Range(0, capacity).Select(i => i * 256).ToArray());
            }
        }

        [Test]
        public void StoppedTargetRetainsIncomingPosition([Values] bool parallel, [Values] bool reverse)
        {
            var source = Normal(1, 64);
            var target = Normal(1, 0);
            source.ConnectTo(target, BeltDirection.Front);
            source.RestoreItems(new[] { State(1, 32) });
            var simulation = Simulation(reverse, source, target);
            for (int tick = 0; tick < 2; tick++)
            {
                simulation.Tick(parallel);
                Assert.That(source.Count, Is.Zero);
                AssertItems(target, new[] { 1 }, new[] { 224 });
            }
        }

        [Test]
        public void ChainUsesNewSpaceOnFollowingTick([Values] bool parallel, [Values] bool reverse)
        {
            var source = Normal(1, 64);
            var middle = Normal(1, 64);
            var target = Normal(2, 64);
            source.ConnectTo(middle, BeltDirection.Front);
            middle.ConnectTo(target, BeltDirection.Front);
            source.RestoreItems(new[] { State(1, 32) });
            middle.RestoreItems(new[] { State(2, 64) });
            var simulation = Simulation(reverse, source, middle, target);
            // 前進でできる空きも、搬出でできる空きも次tickまで使わない。
            // Neither advancing nor outgoing space is usable until the next tick.
            simulation.Tick(parallel);
            AssertItems(source, new[] { 1 }, new[] { 0 });
            AssertItems(middle, new[] { 2 }, new[] { 0 });
            Assert.That(target.Count, Is.Zero);
            simulation.Tick(parallel);
            AssertItems(source, new[] { 1 }, new[] { 0 });
            Assert.That(middle.Count, Is.Zero);
            AssertItems(target, new[] { 2 }, new[] { 448 });
            simulation.Tick(parallel);
            Assert.That(source.Count, Is.Zero);
            AssertItems(middle, new[] { 1 }, new[] { 192 });
            AssertItems(target, new[] { 2 }, new[] { 384 });
        }

        [Test]
        public void SpaceFromTargetAdvanceIsCapturedNextTick([Values] bool parallel, [Values] bool reverse)
        {
            var source = Normal(1, 64);
            var target = Normal(2, 64);
            source.ConnectTo(target, BeltDirection.Front);
            source.RestoreItems(new[] { State(1, 0) });
            target.RestoreItems(new[] { State(2, 224) });
            var simulation = Simulation(reverse, source, target);
            simulation.Tick(parallel);
            AssertItems(source, new[] { 1 }, new[] { 0 });
            AssertItems(target, new[] { 2 }, new[] { 160 });
            simulation.Tick(parallel);
            Assert.That(source.Count, Is.Zero);
            AssertItems(target, new[] { 2, 1 }, new[] { 96, 448 });
        }

        [Test]
        public void ExternalOutputSpaceIsCapturedNextTick([Values] bool parallel, [Values] bool reverse)
        {
            var source = Normal(1, 64);
            var target = Normal(1, 64);
            var receiver = new RecordingReceiver { Offer = 256, Accept = true };
            source.ConnectTo(target, BeltDirection.Front);
            target.ConnectTo(receiver, BeltDirection.Front);
            source.RestoreItems(new[] { State(1, 0) });
            target.RestoreItems(new[] { State(2, 0) });
            var simulation = Simulation(reverse, source, target);
            simulation.Tick(parallel);
            AssertItems(source, new[] { 1 }, new[] { 0 });
            Assert.That(target.Count, Is.Zero);
            Assert.That(receiver.ReceivedGuids, Is.EqualTo(new[] { State(2, 0).Item.Guid }));
            simulation.Tick(parallel);
            Assert.That(source.Count, Is.Zero);
            AssertItems(target, new[] { 1 }, new[] { 192 });
        }

        internal static BeltConveyorSegment Normal(int capacity, int speed)
            => new BeltConveyorSegment(capacity, speed, BeltSegmentKind.Normal, 0);

        internal static BeltItemState State(int id, int distance)
            => new BeltItemState(new BeltItem
            {
                Guid = new Guid(id, 0, 0, new byte[8]), ItemId = id,
                Position = new ItemPosition(new BeltCell(id, -id, 0), BeltEntryDirection.FromBack, 0)
            }, distance);

        static BeltSimulation Simulation(bool reverse, params BeltConveyorSegment[] segments)
            => new BeltSimulation(reverse ? segments.Reverse() : segments);

        static void AssertItems(BeltConveyorSegment segment, int[] ids, int[] distances)
        {
            var actual = segment.CaptureItems();
            Assert.That(actual.Select(state => state.Item.Guid), Is.EqualTo(ids.Select(id => State(id, 0).Item.Guid)));
            Assert.That(actual.Select(state => state.Item.ItemId), Is.EqualTo(ids));
            Assert.That(actual.Select(state => state.DistanceToExit), Is.EqualTo(distances));
        }
    }
}
