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
            var stopped = Create(1, 0, BeltSegmentKind.Branch);
            var moving = Create(1, 64, BeltSegmentKind.Normal);
            var merge = Create(1, 64, BeltSegmentKind.Merge);
            stopped.Buffer.ConnectTo(merge, BeltDirection.Front);
            moving.ConnectTo(merge, BeltDirection.Right);
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
            var side = Create(1, 64, BeltSegmentKind.Normal);
            var straight = Create(1, 64, BeltSegmentKind.Normal);
            var merge = Create(1, 64, BeltSegmentKind.Merge);
            side.ConnectTo(merge, BeltDirection.Right);
            straight.ConnectTo(merge, BeltDirection.Front);
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
            var branch = Create(1, 64, BeltSegmentKind.Branch);
            var front = Create(1, 0, BeltSegmentKind.Normal);
            var left = Create(1, 0, BeltSegmentKind.Normal);
            var right = Create(1, 0, BeltSegmentKind.Normal);
            branch.Buffer.ConnectTo(front, BeltDirection.Front);
            branch.Buffer.ConnectTo(left, BeltDirection.Left);
            branch.Buffer.ConnectTo(right, BeltDirection.Right);
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
            var branch = Create(1, 64, BeltSegmentKind.Branch);
            var front = Create(1, 0, BeltSegmentKind.Normal);
            var left = Create(1, 0, BeltSegmentKind.Normal);
            branch.Buffer.ConnectTo(front, BeltDirection.Front);
            branch.Buffer.ConnectTo(left, BeltDirection.Left);
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
            var branch = Create(1, 64, BeltSegmentKind.Branch);
            var target = Create(3, 64, BeltSegmentKind.Normal);
            branch.Buffer.ConnectTo(target, BeltDirection.Front);
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
            var branch = Create(2, 64, BeltSegmentKind.Branch);
            var target = Create(2, 32, BeltSegmentKind.Normal);
            branch.Buffer.ConnectTo(target, BeltDirection.Front);
            branch.RestoreItems(new[] { new BeltItemState(Item(1), 32), new BeltItemState(Item(2), 320) });
            new BeltSimulation(new[] { branch, target }).Tick();
            Assert.AreEqual(256, branch.CaptureItems()[0].DistanceToExit);
            Assert.AreEqual(416, target.CaptureItems()[0].DistanceToExit);
            Assert.IsFalse(branch.Buffer.HasItem);
        }
    }
}
