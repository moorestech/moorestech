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

        [Test]
        public void MergeRoundRobinAlternatesActualSourceGuids()
        {
            var merge = new BeltConveyorSegment(1, 128, BeltSegmentKind.Merge, 0);
            var receiver = new RecordingReceiver { Offer = 256, Accept = true };
            merge.AttachInput(new ReadySource(), BeltDirection.Left);
            merge.AttachInput(new ReadySource(), BeltDirection.Right);
            merge.Buffer.ConnectTo(receiver, BeltDirection.Front);
            var left = new[]
            {
                BeltSegmentMovementTest.NewItem(1), BeltSegmentMovementTest.NewItem(2),
                BeltSegmentMovementTest.NewItem(3), BeltSegmentMovementTest.NewItem(4)
            };
            var right = new[]
            {
                BeltSegmentMovementTest.NewItem(5), BeltSegmentMovementTest.NewItem(6),
                BeltSegmentMovementTest.NewItem(7), BeltSegmentMovementTest.NewItem(8)
            };

            var simulation = new BeltSimulation(new[] { merge });
            for (var tick = 0; tick < 4; tick++)
            {
                simulation.Tick(false);
                Assert.That(merge.TryReceive(BeltDirection.Left, 128, left[tick]), Is.EqualTo(tick % 2 == 0));
                Assert.That(merge.TryReceive(BeltDirection.Right, 128, right[tick]), Is.EqualTo(tick % 2 == 1));
            }
            simulation.Tick(false);
            Assert.That(receiver.ReceivedGuids,
                Is.EqualTo(new[] { left[0].Guid, right[1].Guid, left[2].Guid, right[3].Guid }));
        }

        [Test]
        public void BranchRoundRobinAdvancesFromStartAfterSkippedOutput()
        {
            var branch = new BeltConveyorSegment(4, 128, BeltSegmentKind.Branch, 0);
            var blocked = new RecordingReceiver { Offer = 0, Accept = true };
            var first = new RecordingReceiver { Offer = 256, Accept = true };
            var second = new RecordingReceiver { Offer = 256, Accept = true };
            branch.AttachInput(new NotReadySource(), BeltDirection.Back);
            branch.Buffer.ConnectTo(blocked, BeltDirection.Front);
            branch.Buffer.ConnectTo(first, BeltDirection.Right);
            branch.Buffer.ConnectTo(second, BeltDirection.Left);
            var items = new[]
            {
                BeltSegmentMovementTest.NewItem(1), BeltSegmentMovementTest.NewItem(2),
                BeltSegmentMovementTest.NewItem(3), BeltSegmentMovementTest.NewItem(4)
            };
            branch.RestoreItems(new[]
            {
                new BeltItemState(items[0], 0), new BeltItemState(items[1], 256),
                new BeltItemState(items[2], 512), new BeltItemState(items[3], 768)
            });

            var simulation = new BeltSimulation(new[] { branch });
            for (var tick = 0; tick < 10; tick++) simulation.Tick(false);
            Assert.That(blocked.ReceivedGuids, Is.Empty);
            Assert.That(first.ReceivedGuids, Is.EqualTo(new[] { items[0].Guid, items[1].Guid, items[3].Guid }));
            Assert.That(second.ReceivedGuids, Is.EqualTo(new[] { items[2].Guid }));
        }

        [Test]
        public void BufferOffersOnlyPriorityMergeUntilThatMergeIsAvailable()
        {
            var branch = new BeltConveyorSegment(1, 32, BeltSegmentKind.Branch, 0);
            var priorityMerge = new BeltConveyorSegment(1, 32, BeltSegmentKind.Merge, 0);
            var alternateMerge = new BeltConveyorSegment(1, 32, BeltSegmentKind.Merge, 0);
            branch.AttachInput(new NotReadySource(), BeltDirection.Back);
            branch.Buffer.ConnectTo(priorityMerge, BeltDirection.Front);
            branch.Buffer.ConnectTo(alternateMerge, BeltDirection.Right);
            priorityMerge.AttachInput(new NotReadySource(), BeltDirection.Left);
            alternateMerge.AttachInput(new NotReadySource(), BeltDirection.Back);
            priorityMerge.Buffer.ConnectTo(new RecordingReceiver { Offer = 256, Accept = true }, BeltDirection.Front);
            alternateMerge.Buffer.ConnectTo(new RecordingReceiver { Offer = 256, Accept = true }, BeltDirection.Front);
            priorityMerge.RestoreItems(new[] { new BeltItemState(BeltSegmentMovementTest.NewItem(1), 128) });
            var pending = BeltSegmentMovementTest.NewItem(2);
            branch.Buffer.RestoreItem(pending);

            var simulation = new BeltSimulation(new[] { branch, priorityMerge, alternateMerge });
            simulation.Tick(false);
            Assert.That(branch.Buffer.TryGetItem(out var retained), Is.True);
            Assert.That(retained.Guid, Is.EqualTo(pending.Guid));
            Assert.That(alternateMerge.Count, Is.Zero);
            for (var tick = 0; tick < 3; tick++) simulation.Tick(false);
            Assert.That(branch.Buffer.HasItem, Is.False);
            Assert.That(priorityMerge.CaptureItems()[0].Item.Guid, Is.EqualTo(pending.Guid));
            Assert.That(alternateMerge.Count, Is.Zero);
        }

        private sealed class ReadySource : IBeltSource
        {
            public bool TryGetOutput(BeltDirection inputDirection) => true;
        }

        private sealed class NotReadySource : IBeltSource
        {
            public bool TryGetOutput(BeltDirection inputDirection) => false;
        }
    }
}
