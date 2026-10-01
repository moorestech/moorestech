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
            var stopped = Segment(network, 0);
            var moving = Segment(network, 1);
            var merge = Segment(network, 2);
            stopped.Buffer.RestoreItem(Item(1));
            moving.RestoreItems(new[] { new BeltItemState(Item(2), 0) });
            var simulation = new BeltSimulation(new[] { stopped, moving, merge });

            // 直進の停止bufferを飛ばして横の搬入を予約する。
            // Skip the stopped straight buffer and reserve the side input.
            simulation.Tick();
            Assert.AreEqual(Item(2), merge.CaptureItems()[0].Item);
            Assert.IsTrue(stopped.Buffer.TryGetItem(out var held));
            Assert.AreEqual(Item(1), held);
            Assert.AreEqual(0, moving.CaptureItems().Length);
        }

        [Test]
        public void MergeUsesDirectionPriorityInsteadOfRegistrationOrderTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (1, 64), (1, 64) },
                new[] { (0, 2, BeltDirection.Right), (1, 2, BeltDirection.Front) });
            var side = Segment(network, 0);
            var straight = Segment(network, 1);
            var merge = Segment(network, 2);
            side.RestoreItems(new[] { new BeltItemState(Item(1), 0) });
            straight.RestoreItems(new[] { new BeltItemState(Item(2), 0) });
            var simulation = new BeltSimulation(new[] { side, straight, merge });
            simulation.Tick();
            Assert.AreEqual(Item(2), merge.CaptureItems()[0].Item);
            Assert.AreEqual(30, merge.PriorityOrder); // (Left, Right, Back)

            // 合流列が空いた次の予約では待っていた横を選ぶ。
            // Once the merge clears, the waiting side wins the next reservation.
            for (int tick = 0; tick < 3; tick++) simulation.Tick();
            Assert.AreEqual(Item(1), merge.CaptureItems()[0].Item);
            Assert.AreEqual(39, merge.PriorityOrder); // (Right, Back, Left)
        }

        [Test]
        public void BranchMovesOnlySuccessfulDirectionToEndTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (1, 0), (1, 0), (1, 0) },
                new[] { (0, 1, BeltDirection.Front), (0, 2, BeltDirection.Left), (0, 3, BeltDirection.Right) });
            var branch = Segment(network, 0);
            var front = Segment(network, 1);
            var left = Segment(network, 2);
            var right = Segment(network, 3);
            front.RestoreItems(new[] { new BeltItemState(Item(1), 0) });
            left.RestoreItems(new[] { new BeltItemState(Item(2), 0) });
            branch.Buffer.RestoreItem(Item(3));
            var simulation = new BeltSimulation(new[] { branch, front, left, right });
            simulation.Tick();
            Assert.AreEqual(Item(3), right.CaptureItems()[0].Item);
            Assert.AreEqual(56, branch.PriorityOrder); // (Front, Left, Right)

            // 末尾方向の成功でも、先行する失敗方向は順序を保つ。
            // Success at the last direction preserves preceding failed directions.
            branch.Buffer.RestoreItem(Item(4));
            simulation.Tick();
            Assert.AreEqual(56, branch.PriorityOrder);
            Assert.IsTrue(branch.Buffer.TryGetItem(out var held));
            Assert.AreEqual(Item(4), held);
        }

        [Test]
        public void BranchSuccessfulMiddleDirectionMovesAfterDisconnectedDirectionTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (1, 0), (1, 0) },
                new[] { (0, 1, BeltDirection.Front), (0, 2, BeltDirection.Left) });
            var branch = Segment(network, 0);
            var front = Segment(network, 1);
            var left = Segment(network, 2);
            front.RestoreItems(new[] { new BeltItemState(Item(1), 0) });
            branch.Buffer.RestoreItem(Item(2));
            new BeltSimulation(new[] { branch, front, left }).Tick();

            // 接続のない右も順序に残し、成功した左だけ末尾へ移す。
            // Retain disconnected right and move only successful left to the end.
            Assert.AreEqual(44, branch.PriorityOrder);
            Assert.AreEqual(Item(2), left.CaptureItems()[0].Item);
        }

        [Test]
        public void BufferFullAtCollectionWaitsUntilNextTickToCollectAgainTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (3, 64) },
                new[] { (0, 1, BeltDirection.Front), (0, -1, BeltDirection.Left) });
            var branch = Segment(network, 0);
            var target = Segment(network, 1);
            branch.Buffer.RestoreItem(Item(1));
            branch.RestoreItems(new[] { new BeltItemState(Item(2), 0) });
            var simulation = new BeltSimulation(new[] { branch, target });

            // 段階3で空いても段階1の回収をやり直さない。
            // Emptying in stage three does not rerun stage one collection.
            simulation.Tick();
            Assert.IsFalse(branch.Buffer.HasItem);
            Assert.AreEqual(1, branch.CaptureItems().Length);
            simulation.Tick();
            Assert.AreEqual(0, branch.CaptureItems().Length);
            Assert.IsTrue(branch.Buffer.HasItem);
        }

        [Test]
        public void BufferCollectionAndNormalInputAdvanceInSameTickTest()
        {
            var network = CreateNetwork(new[] { (2, 64), (2, 32) },
                new[] { (0, 1, BeltDirection.Front), (0, -1, BeltDirection.Left) });
            var branch = Segment(network, 0);
            var target = Segment(network, 1);
            branch.RestoreItems(new[] { new BeltItemState(Item(1), 32), new BeltItemState(Item(2), 320) });
            new BeltSimulation(new[] { branch, target }).Tick();
            Assert.AreEqual(256, branch.CaptureItems()[0].DistanceToExit);
            Assert.AreEqual(416, target.CaptureItems()[0].DistanceToExit);
            Assert.IsFalse(branch.Buffer.HasItem);
        }
    }
}
