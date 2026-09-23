using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Game.BeltSegment;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltSegmentMovementTest
    {
        [Test]
        public void EmptyAndSingleItemRespectZeroAndMaximumSpeed()
        {
            var segment = new BeltConveyorSegment(1, 0, BeltSegmentKind.Normal, 0);
            var simulation = new BeltSimulation(new[] { segment });
            simulation.Tick(false);
            Assert.That(segment.Count, Is.Zero);
            var item = NewItem(1);
            segment.RestoreItems(new[] { new BeltItemState(item, 128) });
            simulation.Tick(false);
            Assert.That(segment.CaptureItems()[0].DistanceToExit, Is.EqualTo(128));
            segment.SetSpeed(128);
            simulation.Tick(false);
            Assert.That(segment.CaptureItems()[0].DistanceToExit, Is.Zero);
            Assert.That(segment.CaptureItems()[0].Item.Guid, Is.EqualTo(item.Guid));
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.SetSpeed(129));
        }

        [Test]
        public void RejectedHeadClampsAndThenFollowerClosesGap()
        {
            var segment = new BeltConveyorSegment(3, 32, BeltSegmentKind.Normal, 0);
            segment.RestoreItems(new[] { new BeltItemState(NewItem(1), 8), new BeltItemState(NewItem(2), 300) });
            var simulation = new BeltSimulation(new[] { segment });
            simulation.Tick(false);
            Assert.That(segment.CaptureItems().Select(x => x.DistanceToExit), Is.EqualTo(new[] { 0, 268 }));
            simulation.Tick(false);
            Assert.That(segment.CaptureItems().Select(x => x.DistanceToExit), Is.EqualTo(new[] { 0, 256 }));
        }

        [Test]
        public void TransferUsesExcessEntryDistanceAndRejectionRetainsItem()
        {
            var source = new BeltConveyorSegment(2, 32, BeltSegmentKind.Normal, 0);
            var receiver = new RecordingReceiver { Offer = 256, Accept = true };
            source.ConnectTo(receiver, BeltDirection.Front);
            source.RestoreItems(new[] { new BeltItemState(NewItem(1), 16) });
            new BeltSimulation(new[] { source }).Tick(false);
            Assert.That(receiver.ReceivedLength, Is.EqualTo(16));
            Assert.That(receiver.ReceivedDirection, Is.EqualTo(BeltDirection.Back));
            Assert.That(source.Count, Is.Zero);

            var rejected = new BeltConveyorSegment(2, 32, BeltSegmentKind.Normal, 0);
            receiver = new RecordingReceiver { Offer = 256, Accept = false };
            rejected.ConnectTo(receiver, BeltDirection.Front);
            rejected.RestoreItems(new[] { new BeltItemState(NewItem(2), 16) });
            new BeltSimulation(new[] { rejected }).Tick(false);
            Assert.That(receiver.ReceivedLength, Is.EqualTo(16));
            Assert.That(rejected.CaptureItems()[0].DistanceToExit, Is.Zero);
        }

        [Test]
        public void EntryDistanceUsesAvailableLengthAndRejectsInsufficientSpace()
        {
            var target = new BeltConveyorSegment(2, 32, BeltSegmentKind.Normal, 0);
            var first = NewItem(1);
            target.RestoreItems(new[] { new BeltItemState(first, 32) });
            Assert.That(target.TryReceive(BeltDirection.Back, 225, NewItem(2)), Is.False);
            Assert.That(target.Count, Is.EqualTo(1));
            Assert.That(target.TryReceive(BeltDirection.Back, 32, NewItem(3)), Is.True);
            Assert.That(target.Count, Is.EqualTo(2));
            Assert.That(target.CaptureItems()[1].DistanceToExit, Is.EqualTo(480));
        }

        [Test]
        public void FullRingCanWrapAfterHeadRemoval()
        {
            var segment = new BeltConveyorSegment(2, 128, BeltSegmentKind.Normal, 0);
            var receiver = new RecordingReceiver { Offer = 256, Accept = true };
            segment.ConnectTo(receiver, BeltDirection.Front);
            var first = NewItem(1);
            var second = NewItem(2);
            var third = NewItem(3);
            segment.RestoreItems(new[] { new BeltItemState(first, 0), new BeltItemState(second, 256) });
            var simulation = new BeltSimulation(new[] { segment });
            simulation.Tick(false);
            Assert.That(segment.Count, Is.EqualTo(1));
            Assert.That(segment.TryReceive(BeltDirection.Back, 128, third), Is.True);
            Assert.That(segment.Count, Is.EqualTo(2));
            Assert.That(segment.CaptureItems().Select(x => x.Item.Guid), Is.EqualTo(new[] { second.Guid, third.Guid }));
            simulation.Tick(false);
            simulation.Tick(false);
            Assert.That(segment.CaptureItems().Select(x => x.Item.Guid), Is.EqualTo(new[] { third.Guid }));
        }

        [Test]
        public void TwelveEntryDirectionsInterpolateFromNeighborCenter()
        {
            var cell = new BeltCell(10, 20, 3);
            var offsets = new[]
            {
                new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(1, 0, 0),
                new Vector3(0, 1, 1), new Vector3(0, -1, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1),
                new Vector3(0, 1, -1), new Vector3(0, -1, -1), new Vector3(-1, 0, -1), new Vector3(1, 0, -1)
            };
            for (int direction = 0; direction < offsets.Length; direction++)
            {
                var position = new ItemPosition(cell, (BeltEntryDirection)direction, 0);
                Assert.That(position.Position, Is.EqualTo(cell.ToPosition() + offsets[direction] * 256));
                position.MoveTo(cell, (BeltEntryDirection)direction, 128);
                Assert.That(position.Position, Is.EqualTo(cell.ToPosition() + offsets[direction] * 128));
                position.MoveTo(cell, (BeltEntryDirection)direction, 256);
                Assert.That(position.Position, Is.EqualTo(cell.ToPosition()));
            }
        }

        internal static BeltItem NewItem(int id) => new BeltItem { Guid = Guid.NewGuid(), ItemId = id };
    }

    internal sealed class RecordingReceiver : IBeltReceiver
    {
        internal int Offer;
        internal bool Accept;
        internal int ReceivedLength;
        internal BeltDirection ReceivedDirection;
        internal readonly List<Guid> ReceivedGuids = new List<Guid>();

        public void AttachInput(IBeltSource source, BeltDirection inputDirection) { }
        public int GetOffer(BeltDirection inputDirection) => Offer;
        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            ReceivedDirection = inputDirection;
            ReceivedLength = length;
            if (Accept) ReceivedGuids.Add(item.Guid);
            return Accept;
        }
    }
}
