using System;
using Game.BeltSegment;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltSegmentJunctionTest
    {
        [Test]
        public void BranchArrivalAdvancesAgainInNormalPhase()
        {
            var branch = new BeltConveyorSegment(1, 32, BeltSegmentKind.Branch, 0);
            var target = new BeltConveyorSegment(2, 32, BeltSegmentKind.Normal, 0);
            var alternate = new BeltConveyorSegment(2, 32, BeltSegmentKind.Normal, 0);
            branch.AttachInput(new ReadySource(), BeltDirection.Back);
            branch.Buffer.ConnectTo(target, BeltDirection.Front);
            branch.Buffer.ConnectTo(alternate, BeltDirection.Right);
            branch.RestoreItems(new[] { new BeltItemState(new BeltItem { Guid = Guid.NewGuid(), ItemId = 7 }, 16) });
            new BeltSimulation(new[] { branch, target, alternate }).Tick(false);
            Assert.That(branch.Count, Is.Zero);
            Assert.That(branch.Buffer.HasItem, Is.False);
            Assert.That(target.CaptureItems()[0].DistanceToExit, Is.EqualTo(448));
        }

        [Test]
        public void MergeAcceptsOnlyReservedDirectionAndReservationDoesNotRotate()
        {
            var merge = new BeltConveyorSegment(1, 32, BeltSegmentKind.Merge, 0);
            merge.AttachInput(new ReadySource(), BeltDirection.Left);
            merge.AttachInput(new ReadySource(), BeltDirection.Right);
            merge.Buffer.ConnectTo(new RecordingReceiver { Offer = 256, Accept = true }, BeltDirection.Front);
            new BeltSimulation(new[] { merge }).Tick(false);
            Assert.That(merge.PriorityIndex, Is.Zero);
            Assert.That(merge.GetOffer(BeltDirection.Right), Is.Zero);
            Assert.That(merge.TryReceive(BeltDirection.Right, 32, BeltSegmentMovementTest.NewItem(1)), Is.False);
            Assert.That(merge.TryReceive(BeltDirection.Left, 32, BeltSegmentMovementTest.NewItem(2)), Is.True);
            Assert.That(merge.PriorityIndex, Is.EqualTo(1));
        }

        [Test]
        public void SkippedOutputAdvancesRoundRobinOnlyOneIndex()
        {
            var branch = new BeltConveyorSegment(1, 32, BeltSegmentKind.Branch, 0);
            var blocked = new RecordingReceiver { Offer = 0, Accept = true };
            var accepted = new RecordingReceiver { Offer = 256, Accept = true };
            var later = new RecordingReceiver { Offer = 256, Accept = true };
            branch.AttachInput(new ReadySource(), BeltDirection.Left);
            branch.Buffer.ConnectTo(blocked, BeltDirection.Front);
            branch.Buffer.ConnectTo(accepted, BeltDirection.Right);
            branch.Buffer.ConnectTo(later, BeltDirection.Back);
            branch.Buffer.RestoreItem(BeltSegmentMovementTest.NewItem(1));
            new BeltSimulation(new[] { branch }).Tick(false);
            Assert.That(accepted.ReceivedLength, Is.EqualTo(32));
            Assert.That(later.ReceivedLength, Is.Zero);
            Assert.That(branch.PriorityIndex, Is.EqualTo(1));
        }

        [Test]
        public void BufferFullAtCollectionPhaseDoesNotCollectAgainAfterTransfer()
        {
            var branch = new BeltConveyorSegment(1, 32, BeltSegmentKind.Branch, 0);
            var receiver = new RecordingReceiver { Offer = 256, Accept = true };
            branch.AttachInput(new ReadySource(), BeltDirection.Left);
            branch.Buffer.ConnectTo(receiver, BeltDirection.Front);
            branch.Buffer.ConnectTo(new RecordingReceiver { Offer = 256, Accept = true }, BeltDirection.Right);
            branch.Buffer.RestoreItem(BeltSegmentMovementTest.NewItem(1));
            var waiting = BeltSegmentMovementTest.NewItem(2);
            branch.RestoreItems(new[] { new BeltItemState(waiting, 0) });
            new BeltSimulation(new[] { branch }).Tick(false);
            Assert.That(branch.Buffer.HasItem, Is.False);
            Assert.That(branch.Count, Is.EqualTo(1));
            Assert.That(branch.CaptureItems()[0].Item.Guid, Is.EqualTo(waiting.Guid));
            Assert.That(branch.CaptureItems()[0].DistanceToExit, Is.Zero);
        }

        private sealed class ReadySource : IBeltSource
        {
            public bool TryGetOutput(BeltDirection inputDirection) => true;
        }
    }
}
