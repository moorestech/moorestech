using System;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltTransportMovementTest
    {
        [Test]
        public void FullSelfConnectedLoopStopsTest()
        {
            var network = CreateNetwork(new[] { (3, 128) }, new[] { (0, 0, BeltDirection.Front) });
            var loop = Segment(network, 0);
            var initial = new[] { new BeltItemState(Item(1), 0), new BeltItemState(Item(2), 256), new BeltItemState(Item(3), 512) };
            loop.RestoreItems(initial);
            var simulation = new BeltSimulation(new[] { loop });

            // 全周満杯なら切れ目でも空きが生じない。
            // A full loop cannot create space at its seam.
            for (int tick = 0; tick < 20; tick++) simulation.Tick();
            CollectionAssert.AreEqual(initial, loop.CaptureItems());
        }

        [Test]
        public void SelfConnectedLoopWrapsAndPreservesIdentityTest()
        {
            var network = CreateNetwork(new[] { (3, 64) }, new[] { (0, 0, BeltDirection.Front) });
            var loop = Segment(network, 0);
            loop.RestoreItems(new[] { new BeltItemState(Item(1), 0) });
            var simulation = new BeltSimulation(new[] { loop });
            simulation.Tick();
            Assert.AreEqual(704, loop.CaptureItems()[0].DistanceToExit);

            // 1周後も同じ個体が同じ整数位置へ戻る。
            // The same instance returns to its integer position after a lap.
            for (int tick = 0; tick < 11; tick++) simulation.Tick();
            Assert.AreEqual(new BeltItemState(Item(1), 0), loop.CaptureItems()[0]);
        }

        [TestCase(64, 64)]
        [TestCase(128, 32)]
        [TestCase(32, 128)]
        public void SegmentBoundaryUsesSourceSpeedAndDefersTargetMovementTest(int sourceSpeed, int targetSpeed)
        {
            var network = CreateNetwork(new[] { (1, sourceSpeed), (2, targetSpeed) }, new[] { (0, 1, BeltDirection.Front) });
            var source = Segment(network, 0);
            var target = Segment(network, 1);
            source.RestoreItems(new[] { new BeltItemState(Item(1), 0) });
            var simulation = new BeltSimulation(new[] { target, source });
            simulation.Tick();
            Assert.AreEqual(0, source.CaptureItems().Length);
            Assert.AreEqual(512 - sourceSpeed, target.CaptureItems()[0].DistanceToExit);
            simulation.Tick();
            Assert.AreEqual(512 - sourceSpeed - targetSpeed, target.CaptureItems()[0].DistanceToExit);
        }

        [Test]
        public void SpaceCreatedDuringMovementIsAvailableNextTickTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (2, 64) }, new[] { (0, 1, BeltDirection.Front) });
            var source = Segment(network, 0);
            var target = Segment(network, 1);
            source.RestoreItems(new[] { new BeltItemState(Item(1), 0) });
            target.RestoreItems(new[] { new BeltItemState(Item(2), 256) });
            var simulation = new BeltSimulation(new[] { target, source });

            // 前進で生まれる空きを同tickの搬送に使わない。
            // New space from movement is unavailable within the same tick.
            simulation.Tick();
            Assert.AreEqual(1, source.CaptureItems().Length);
            Assert.AreEqual(192, target.CaptureItems()[0].DistanceToExit);
            simulation.Tick();
            Assert.AreEqual(0, source.CaptureItems().Length);
            Assert.AreEqual(448, target.CaptureItems()[1].DistanceToExit);
        }

        [Test]
        public void BlockedHeadCompactsFollowingBlocksTest()
        {
            var network = CreateNetwork(new[] { (4, 64) }, Array.Empty<(int, int, BeltDirection)>());
            var belt = Segment(network, 0);
            belt.RestoreItems(new[] { new BeltItemState(Item(1), 0), new BeltItemState(Item(2), 280), new BeltItemState(Item(3), 600) });
            var simulation = new BeltSimulation(new[] { belt });
            simulation.Tick();
            CollectionAssert.AreEqual(new[] { new BeltItemState(Item(1), 0), new BeltItemState(Item(2), 256), new BeltItemState(Item(3), 536) }, belt.CaptureItems());
            simulation.Tick();
            Assert.AreEqual(512, belt.CaptureItems()[2].DistanceToExit);
        }

        [TestCase(-1)]
        [TestCase(129)]
        public void InvalidSpeedRejectedTest(int speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateNetwork(new[] { (1, speed) }, Array.Empty<(int, int, BeltDirection)>()));
        }
    }
}
