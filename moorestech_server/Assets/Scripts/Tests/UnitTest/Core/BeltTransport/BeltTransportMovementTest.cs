using System.Linq;
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
            SetItems(network, new[] { State(3, 256, 1, false), State(2, 256, 2, false), State(1, 256, 3, false) });
            var initial = network.CaptureItems();
            // 全周満杯なら切れ目でも空きが生じない。
            // A full loop cannot create space at its seam.
            for (int tick = 0; tick < 20; tick++) network.Tick();
            CollectionAssert.AreEqual(initial, network.CaptureItems());
        }

        [Test]
        public void SelfConnectedLoopWrapsAndPreservesIdentityTest()
        {
            var network = CreateNetwork(new[] { (3, 64) }, new[] { (0, 0, BeltDirection.Front) });
            SetItems(network, new[] { State(3, 256, 1, false) });
            var initial = network.CaptureItems();
            network.Tick();
            Assert.AreEqual(1, network.CaptureItems()[0].CellId);
            Assert.AreEqual(64, network.CaptureItems()[0].Progress);
            // 1周後も同じ個体が同じ整数位置へ戻る。
            // The same instance returns to its integer position after a lap.
            for (int tick = 0; tick < 11; tick++) network.Tick();
            CollectionAssert.AreEqual(initial, network.CaptureItems());
        }

        [TestCase(64, 64)]
        [TestCase(128, 32)]
        [TestCase(32, 128)]
        public void SegmentBoundaryUsesSourceSpeedAndDefersTargetMovementTest(int sourceSpeed, int targetSpeed)
        {
            var network = CreateNetwork(new[] { (1, sourceSpeed), (2, targetSpeed) }, new[] { (0, 1, BeltDirection.Front) });
            SetItems(network, new[] { State(1, 256, 1, false) });
            network.Tick();
            var transferred = network.CaptureItems().Single();
            Assert.AreEqual(101, transferred.CellId);
            Assert.AreEqual(sourceSpeed, transferred.Progress);
            Assert.AreEqual(Item(1), transferred.Item);
            network.Tick();
            Assert.AreEqual(sourceSpeed + targetSpeed, network.CaptureItems().Single().Progress);
        }

        [Test]
        public void SpaceCreatedDuringMovementIsAvailableNextTickTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (2, 64) }, new[] { (0, 1, BeltDirection.Front) });
            SetItems(network, new[] { State(1, 256, 1, false), State(101, 256, 2, false) });
            // 前進で生まれる空きを同tickの搬送に使わない。
            // New space from movement is unavailable within the same tick.
            network.Tick();
            Assert.AreEqual(1, network.CaptureItems().Single(value => value.Item.Guid == Item(1).Guid).CellId);
            Assert.AreEqual(64, network.CaptureItems().Single(value => value.Item.Guid == Item(2).Guid).Progress);
            network.Tick();
            var transferred = network.CaptureItems().Single(value => value.Item.Guid == Item(1).Guid);
            Assert.AreEqual(101, transferred.CellId);
            Assert.AreEqual(64, transferred.Progress);
        }

        [Test]
        public void BlockedHeadCompactsFollowingBlocksTest()
        {
            var network = CreateNetwork(new[] { (4, 64) }, System.Array.Empty<(int, int, BeltDirection)>());
            SetItems(network, new[] { State(4, 256, 1, false), State(3, 232, 2, false), State(2, 168, 3, false) });
            network.Tick();
            CollectionAssert.AreEqual(new[] { State(4, 256, 1, false), State(3, 256, 2, false), State(2, 232, 3, false) }, network.CaptureItems());
            network.Tick();
            Assert.AreEqual(256, network.CaptureItems().Single(value => value.Item.Guid == Item(3).Guid).Progress);
        }
    }
}
