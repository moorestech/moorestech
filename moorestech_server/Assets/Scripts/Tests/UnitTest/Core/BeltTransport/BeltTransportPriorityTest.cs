using System.Linq;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltTransportPriorityTest
    {
        [Test]
        public void StoppedBufferCannotReserveMergeTest()
        {
            var network = CreateNetwork(new[] { (1, 0), (1, 64), (1, 64) },
                new[] { (0, 2, BeltDirection.Front), (0, -1, BeltDirection.Left), (1, 2, BeltDirection.Right) });
            SetItems(network, new[] { State(1, 256, 1, true), State(101, 256, 2, false) });
            // 停止bufferを飛ばして横の搬入を予約する。
            // Skip the stopped buffer and reserve the side input.
            network.Tick();
            var received = network.CaptureItems().Single(value => value.Item.Guid == Item(2).Guid);
            Assert.AreEqual(201, received.CellId);
            Assert.IsFalse(received.IsBuffer);
            Assert.AreEqual(1, network.CaptureItems().Single(value => value.Item.Guid == Item(1).Guid).CellId);
            Assert.IsTrue(network.CaptureItems().Single(value => value.Item.Guid == Item(1).Guid).IsBuffer);
        }

        [Test]
        public void MergeUsesDirectionPriorityInsteadOfRegistrationOrderTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (1, 64), (1, 64) },
                new[] { (0, 2, BeltDirection.Right), (1, 2, BeltDirection.Front) });
            SetItems(network, new[] { State(1, 256, 1, false), State(101, 256, 2, false) });
            network.Tick();
            Assert.AreEqual(Item(2), network.CaptureItems().Single(value => value.CellId == 201).Item);
            Assert.AreEqual(30, network.GetPriority(201));
            // 合流列が空いた次の予約では待っていた横を選ぶ。
            // Once the merge clears, the waiting side wins the next reservation.
            for (int tick = 0; tick < 3; tick++) network.Tick();
            Assert.AreEqual(Item(1), network.CaptureItems().Single(value => value.CellId == 201 && !value.IsBuffer).Item);
            Assert.AreEqual(39, network.GetPriority(201));
        }

        [Test]
        public void BranchMovesOnlySuccessfulDirectionToEndTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (1, 0), (1, 0), (1, 0) },
                new[] { (0, 1, BeltDirection.Front), (0, 2, BeltDirection.Left), (0, 3, BeltDirection.Right) });
            SetItems(network, new[] { State(101, 256, 1, false), State(201, 256, 2, false), State(1, 256, 3, true) });
            network.Tick();
            Assert.AreEqual(Item(3), network.CaptureItems().Single(value => value.CellId == 301).Item);
            Assert.AreEqual(56, network.GetPriority(1));
            // 先行する失敗方向は順序を保つ。
            // Preserve preceding failed directions after a successful transfer.
            network.ReplaceCellItems(1, new[] { State(1, 256, 4, true) });
            network.Tick();
            Assert.AreEqual(56, network.GetPriority(1));
            Assert.AreEqual(Item(4), network.CaptureItems().Single(value => value.CellId == 1 && value.IsBuffer).Item);
        }

        [Test]
        public void BranchSuccessfulMiddleDirectionMovesAfterDisconnectedDirectionTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (1, 0), (1, 0) },
                new[] { (0, 1, BeltDirection.Front), (0, 2, BeltDirection.Left) });
            SetItems(network, new[] { State(101, 256, 1, false), State(1, 256, 2, true) });
            network.Tick();
            Assert.AreEqual(44, network.GetPriority(1));
            Assert.AreEqual(Item(2), network.CaptureItems().Single(value => value.CellId == 201).Item);
        }

        [Test]
        public void BufferFullAtCollectionWaitsUntilNextTickToCollectAgainTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (3, 64) },
                new[] { (0, 1, BeltDirection.Front), (0, -1, BeltDirection.Left) });
            SetItems(network, new[] { State(1, 256, 1, true), State(1, 256, 2, false) });
            // 段階3で空いても段階1の回収をやり直さない。
            // Emptying in stage three does not rerun stage one collection.
            network.Tick();
            Assert.IsFalse(network.CaptureItems().Single(value => value.CellId == 1).IsBuffer);
            network.Tick();
            Assert.IsTrue(network.CaptureItems().Single(value => value.CellId == 1).IsBuffer);
        }

        [Test]
        public void BufferCollectionAndNormalInputAdvanceInSameTickTest()
        {
            var network = CreateNetwork(new[] { (2, 64), (2, 32) },
                new[] { (0, 1, BeltDirection.Front), (0, -1, BeltDirection.Left) });
            SetItems(network, new[] { State(2, 224, 1, false), State(1, 192, 2, false) });
            network.Tick();
            var items = network.CaptureItems();
            Assert.AreEqual(256, items.Single(value => value.Item.Guid == Item(2).Guid).Progress);
            Assert.AreEqual(96, items.Single(value => value.Item.Guid == Item(1).Guid).Progress);
            Assert.IsFalse(items.Any(value => value.IsBuffer));
        }
    }
}
