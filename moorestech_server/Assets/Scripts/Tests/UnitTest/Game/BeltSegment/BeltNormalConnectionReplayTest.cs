using System.Linq;
using Game.BeltSegment;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltNormalConnectionReplayTest
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        public void ConnectionsPreserveEveryItemAcrossOrdersAndParallelTicks(int segmentCount)
        {
            var serial = BuildNetwork(segmentCount);
            var reverse = BuildNetwork(segmentCount);
            var parallel = BuildNetwork(segmentCount);
            var reverseParallel = BuildNetwork(segmentCount);
            var expectedIds = AllIds(serial);
            var simulations = new[]
            {
                new BeltSimulation(serial), new BeltSimulation(reverse.Reverse()),
                new BeltSimulation(parallel), new BeltSimulation(reverseParallel.Reverse())
            };
            // 同一の配線を4通りで走らせ、各segment内の順序まで比較する。
            // Run identical wiring four ways and compare order within every segment.
            for (int tick = 0; tick < 200; tick++)
            {
                for (int i = 0; i < simulations.Length; i++) simulations[i].Tick(i >= 2);
                AssertSame(serial, reverse);
                AssertSame(serial, parallel);
                AssertSame(serial, reverseParallel);
                Assert.That(AllIds(serial), Is.EquivalentTo(expectedIds), $"tick {tick}");
            }
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(6, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(6, true)]
        public void RebuiltSimulationContinuesCapturedItemsBuffersAndRoundRobin(int segmentCount, bool parallel)
        {
            var original = BuildNetwork(segmentCount);
            var simulation = new BeltSimulation(original);
            for (int tick = 0; tick < 80; tick++) simulation.Tick(parallel);
            var restored = original.Select(segment => new BeltConveyorSegment(
                segment.Capacity, segment.Speed, segment.Kind, segment.PriorityIndex)).ToArray();
            Wire(restored);
            // tick境界では既存の保存状態だけで再開できる。
            // Existing captured state suffices to resume at a tick boundary.
            for (int i = 0; i < original.Length; i++)
            {
                restored[i].RestoreItems(original[i].CaptureItems());
                if (original[i].Buffer != null && original[i].Buffer.TryGetItem(out var item))
                    restored[i].Buffer.RestoreItem(item);
            }
            var replay = new BeltSimulation(restored.Reverse());
            AssertSame(original, restored);
            for (int tick = 0; tick < 80; tick++)
            {
                simulation.Tick(parallel);
                replay.Tick(parallel);
                AssertSame(original, restored);
            }
        }

        [Test]
        public void FullNormalCycleStops([Values] bool parallel, [Values] bool reverse)
        {
            var segments = new[] { Normal(1), Normal(1), Normal(1) };
            Wire(segments);
            for (int i = 0; i < segments.Length; i++)
                segments[i].RestoreItems(new[] { BeltNormalConnectionTest.State(i + 1, 0) });
            var simulation = new BeltSimulation(reverse ? segments.Reverse() : segments);
            for (int tick = 0; tick < 10; tick++)
            {
                simulation.Tick(parallel);
                for (int i = 0; i < segments.Length; i++)
                {
                    var states = segments[i].CaptureItems();
                    Assert.That(states.Length, Is.EqualTo(1));
                    Assert.That(states[0].Item.Guid, Is.EqualTo(BeltNormalConnectionTest.State(i + 1, 0).Item.Guid));
                    Assert.That(states[0].DistanceToExit, Is.Zero);
                }
            }
        }

        static BeltConveyorSegment[] BuildNetwork(int count)
        {
            var segments = Enumerable.Range(0, count).Select(i => Normal(4)).ToArray();
            if (count == 6)
            {
                segments[4] = new BeltConveyorSegment(1, 64, BeltSegmentKind.Merge, 1);
                segments[5] = new BeltConveyorSegment(1, 64, BeltSegmentKind.Branch, 1);
            }
            Wire(segments);
            segments[0].RestoreItems(new[] { BeltNormalConnectionTest.State(1, 32), BeltNormalConnectionTest.State(2, 400) });
            if (count > 1) segments[1].RestoreItems(new[] { BeltNormalConnectionTest.State(3, 96) });
            if (count == 6)
            {
                segments[2].RestoreItems(new[] { BeltNormalConnectionTest.State(4, 128) });
                segments[4].Buffer.RestoreItem(BeltNormalConnectionTest.State(5, 0).Item);
                segments[5].Buffer.RestoreItem(BeltNormalConnectionTest.State(6, 0).Item);
            }
            return segments;
        }

        static void Wire(BeltConveyorSegment[] segments)
        {
            if (segments.Length < 6)
            {
                for (int i = 0; i < segments.Length; i++)
                    segments[i].ConnectTo(segments[(i + 1) % segments.Length], BeltDirection.Front);
                return;
            }
            // 二つのNormal連鎖を合流・分岐で閉じ、既存buffer段階と共存させる。
            // Close two Normal chains through merge and branch buffer phases.
            segments[0].ConnectTo(segments[1], BeltDirection.Front);
            segments[1].ConnectTo(segments[4], BeltDirection.Right);
            segments[2].ConnectTo(segments[3], BeltDirection.Front);
            segments[3].ConnectTo(segments[4], BeltDirection.Left);
            segments[4].Buffer.ConnectTo(segments[5], BeltDirection.Front);
            segments[5].Buffer.ConnectTo(segments[0], BeltDirection.Left);
            segments[5].Buffer.ConnectTo(segments[2], BeltDirection.Right);
        }

        static BeltConveyorSegment Normal(int capacity) => BeltNormalConnectionTest.Normal(capacity, 64);

        static System.Guid[] AllIds(BeltConveyorSegment[] segments)
            => segments.SelectMany(segment => segment.CaptureItems().Select(state => state.Item.Guid)
                .Concat(segment.Buffer != null && segment.Buffer.TryGetItem(out var item)
                    ? new[] { item.Guid } : System.Array.Empty<System.Guid>())).ToArray();

        static void AssertSame(BeltConveyorSegment[] expected, BeltConveyorSegment[] actual)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                var left = expected[i].CaptureItems();
                var right = actual[i].CaptureItems();
                Assert.That(right.Length, Is.EqualTo(left.Length), $"segment {i}");
                for (int j = 0; j < left.Length; j++)
                {
                    Assert.That(right[j].DistanceToExit, Is.EqualTo(left[j].DistanceToExit));
                    AssertItem(left[j].Item, right[j].Item);
                }
                Assert.That(actual[i].PriorityIndex, Is.EqualTo(expected[i].PriorityIndex));
                if (expected[i].Buffer == null) continue;
                bool hasItem = expected[i].Buffer.TryGetItem(out var expectedItem);
                Assert.That(actual[i].Buffer.TryGetItem(out var actualItem), Is.EqualTo(hasItem));
                if (hasItem) AssertItem(expectedItem, actualItem);
            }
        }

        static void AssertItem(BeltItem expected, BeltItem actual)
        {
            Assert.That(actual.Guid, Is.EqualTo(expected.Guid));
            Assert.That(actual.ItemId, Is.EqualTo(expected.ItemId));
            Assert.That(actual.Position.CurrentCell, Is.EqualTo(expected.Position.CurrentCell));
            Assert.That(actual.Position.EntryDirection, Is.EqualTo(expected.Position.EntryDirection));
            Assert.That(actual.Position.Progress, Is.EqualTo(expected.Position.Progress));
            Assert.That(actual.Position.Position, Is.EqualTo(expected.Position.Position));
        }
    }
}
