using System;
using System.Linq;
using Game.BeltSegment;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltSegmentReplayTest
    {
        [Test]
        public void CaptureAndRestorePreserveIdentityOrderGapsBufferAndPriority()
        {
            var branch = new BeltConveyorSegment(3, 64, BeltSegmentKind.Branch, 1);
            var position = new ItemPosition(new BeltCell(3, 4, -1), BeltEntryDirection.FromLeftBelow, 128);
            var states = new[]
            {
                new BeltItemState(NewItem(1, position), 16),
                new BeltItemState(NewItem(2, position), 300),
                new BeltItemState(NewItem(3, position), 600)
            };
            branch.RestoreItems(states);
            var buffered = NewItem(4, position);
            branch.Buffer.RestoreItem(buffered);

            var replay = new BeltConveyorSegment(branch.Capacity, branch.Speed, BeltSegmentKind.Branch, branch.PriorityIndex);
            replay.RestoreItems(branch.CaptureItems());
            if (branch.Buffer.TryGetItem(out var captured)) replay.Buffer.RestoreItem(captured);
            Assert.That(replay.CaptureItems().Select(x => x.Item.Guid), Is.EqualTo(states.Select(x => x.Item.Guid)));
            Assert.That(replay.CaptureItems().Select(x => x.Item.ItemId), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(replay.CaptureItems().Select(x => x.DistanceToExit), Is.EqualTo(new[] { 16, 300, 600 }));
            Assert.That(replay.CaptureItems()[0].Item.Position, Is.SameAs(position));
            Assert.That(replay.Buffer.TryGetItem(out var replayed), Is.True);
            Assert.That(replayed.Guid, Is.EqualTo(buffered.Guid));
            Assert.That(replay.PriorityIndex, Is.EqualTo(1));
        }

        [Test]
        public void ClosedMergeBranchNetworkMatchesSerialAndParallelForManyTicks()
        {
            var serial = BuildNetwork();
            var parallel = BuildNetwork();
            var serialSimulation = new BeltSimulation(serial);
            var parallelSimulation = new BeltSimulation(parallel);
            for (int tick = 0; tick < 80; tick++)
            {
                serialSimulation.Tick(false);
                parallelSimulation.Tick(true);
                Assert.That(Snapshot(parallel), Is.EqualTo(Snapshot(serial)), $"tick {tick}");
            }
        }

        private static BeltConveyorSegment[] BuildNetwork()
        {
            var left = new BeltConveyorSegment(3, 64, BeltSegmentKind.Normal, 0);
            var right = new BeltConveyorSegment(3, 64, BeltSegmentKind.Normal, 0);
            var merge = new BeltConveyorSegment(1, 64, BeltSegmentKind.Merge, 0);
            var branch = new BeltConveyorSegment(1, 64, BeltSegmentKind.Branch, 0);
            left.ConnectTo(merge, BeltDirection.Right);
            right.ConnectTo(merge, BeltDirection.Left);
            merge.Buffer.ConnectTo(branch, BeltDirection.Front);
            branch.Buffer.ConnectTo(left, BeltDirection.Left);
            branch.Buffer.ConnectTo(right, BeltDirection.Right);
            left.RestoreItems(new[] { new BeltItemState(StableItem(1), 32), new BeltItemState(StableItem(2), 400) });
            right.RestoreItems(new[] { new BeltItemState(StableItem(3), 96) });
            branch.RestoreItems(new[] { new BeltItemState(StableItem(4), 128) });
            return new[] { left, right, merge, branch };
        }

        private static string Snapshot(BeltConveyorSegment[] segments)
        {
            return string.Join("|", segments.Select(segment =>
            {
                var items = string.Join(",", segment.CaptureItems().Select(state =>
                    $"{state.Item.Guid}:{state.Item.ItemId}:{state.DistanceToExit}"));
                var buffer = segment.Buffer != null && segment.Buffer.TryGetItem(out var item)
                    ? item.Guid.ToString() : "empty";
                return $"{segment.Kind}:{segment.PriorityIndex}:{items}:{buffer}";
            }));
        }

        private static BeltItem NewItem(int id, ItemPosition position)
            => new BeltItem { Guid = Guid.NewGuid(), ItemId = id, Position = position };

        private static BeltItem StableItem(int id)
            => new BeltItem { Guid = new Guid(id, 0, 0, new byte[8]), ItemId = id };
    }
}
