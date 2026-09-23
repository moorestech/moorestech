using System.Linq;
using Game.BeltSegment;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltNormalConnectionReplayTest
    {
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(6, true)]
        public void ConnectionsPreserveEveryItemAcrossOrdersAndParallelTicks(int segmentCount, bool mixedNetwork)
        {
            var serial = mixedNetwork ? BuildMixedNetwork() : BuildNormalCycle(segmentCount);
            var reverse = mixedNetwork ? BuildMixedNetwork() : BuildNormalCycle(segmentCount);
            var parallel = mixedNetwork ? BuildMixedNetwork() : BuildNormalCycle(segmentCount);
            var reverseParallel = mixedNetwork ? BuildMixedNetwork() : BuildNormalCycle(segmentCount);
            var expectedIds = AllIds();
            var simulations = new (BeltSimulation Simulation, bool Parallel)[]
            {
                (new BeltSimulation(serial), false), (new BeltSimulation(reverse.Reverse()), false),
                (new BeltSimulation(parallel), true), (new BeltSimulation(reverseParallel.Reverse()), true)
            };
            // 同一配線を4方式で列内順序比較。
            // Compare item order in each segment across four runs of identical wiring.
            for (int tick = 0; tick < 200; tick++)
            {
                foreach (var entry in simulations) entry.Simulation.Tick(entry.Parallel);
                AssertSame(serial, reverse);
                AssertSame(serial, parallel);
                AssertSame(serial, reverseParallel);
                Assert.That(AllIds(), Is.EquivalentTo(expectedIds), $"tick {tick}");
            }

            #region Internal

            System.Guid[] AllIds()
                => serial.SelectMany(segment => segment.CaptureItems().Select(state => state.Item.Guid)
                    .Concat(segment.Buffer != null && segment.Buffer.TryGetItem(out var item)
                        ? new[] { item.Guid } : System.Array.Empty<System.Guid>())).ToArray();

            #endregion
        }

        [TestCase(1, false, false)]
        [TestCase(2, false, false)]
        [TestCase(3, false, false)]
        [TestCase(6, true, false)]
        [TestCase(1, false, true)]
        [TestCase(2, false, true)]
        [TestCase(3, false, true)]
        [TestCase(6, true, true)]
        public void RebuiltSimulationContinuesCapturedItemsBuffersAndRoundRobin(int segmentCount, bool mixedNetwork, bool parallel)
        {
            var original = mixedNetwork ? BuildMixedNetwork() : BuildNormalCycle(segmentCount);
            var simulation = new BeltSimulation(original);
            for (int tick = 0; tick < 80; tick++) simulation.Tick(parallel);
            var restored = original.Select(segment => new BeltConveyorSegment(
                segment.Capacity, segment.Speed, segment.Kind, segment.PriorityIndex)).ToArray();
            if (mixedNetwork) WireMixedNetwork(restored);
            else WireNormalCycle(restored);
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
            WireNormalCycle(segments);
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

        static BeltConveyorSegment[] BuildNormalCycle(int count)
        {
            var segments = Enumerable.Range(0, count).Select(i => Normal(4)).ToArray();
            WireNormalCycle(segments);
            segments[0].RestoreItems(new[] { BeltNormalConnectionTest.State(1, 32), BeltNormalConnectionTest.State(2, 400) });
            if (1 < count) segments[1].RestoreItems(new[] { BeltNormalConnectionTest.State(3, 96) });
            return segments;
        }

        static BeltConveyorSegment[] BuildMixedNetwork()
        {
            var segments = new[]
            {
                Normal(4), Normal(4), Normal(4), Normal(4),
                new BeltConveyorSegment(1, 64, BeltSegmentKind.Merge, 1),
                new BeltConveyorSegment(1, 64, BeltSegmentKind.Branch, 1)
            };
            WireMixedNetwork(segments);
            segments[0].RestoreItems(new[] { BeltNormalConnectionTest.State(1, 32), BeltNormalConnectionTest.State(2, 400) });
            segments[1].RestoreItems(new[] { BeltNormalConnectionTest.State(3, 96) });
            segments[2].RestoreItems(new[] { BeltNormalConnectionTest.State(4, 128) });
            segments[4].Buffer.RestoreItem(BeltNormalConnectionTest.State(5, 0).Item);
            segments[5].Buffer.RestoreItem(BeltNormalConnectionTest.State(6, 0).Item);
            return segments;
        }

        static void WireNormalCycle(BeltConveyorSegment[] segments)
        {
            for (int i = 0; i < segments.Length; i++)
                segments[i].ConnectTo(segments[(i + 1) % segments.Length], BeltDirection.Front);
        }

        static void WireMixedNetwork(BeltConveyorSegment[] segments)
        {
            // 2連鎖を合流・分岐で環状化。
            // Close two chains into a cycle through merge and branch.
            segments[0].ConnectTo(segments[1], BeltDirection.Front);
            segments[1].ConnectTo(segments[4], BeltDirection.Right);
            segments[2].ConnectTo(segments[3], BeltDirection.Front);
            segments[3].ConnectTo(segments[4], BeltDirection.Left);
            segments[4].Buffer.ConnectTo(segments[5], BeltDirection.Front);
            segments[5].Buffer.ConnectTo(segments[0], BeltDirection.Left);
            segments[5].Buffer.ConnectTo(segments[2], BeltDirection.Right);
        }

        static BeltConveyorSegment Normal(int capacity) => BeltNormalConnectionTest.Normal(capacity, 64);

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

            #region Internal

            void AssertItem(BeltItem expectedItem, BeltItem actualItem)
            {
                Assert.That(actualItem.Guid, Is.EqualTo(expectedItem.Guid));
                Assert.That(actualItem.ItemId, Is.EqualTo(expectedItem.ItemId));
                Assert.That(actualItem.Position.CurrentCell, Is.EqualTo(expectedItem.Position.CurrentCell));
                Assert.That(actualItem.Position.EntryDirection, Is.EqualTo(expectedItem.Position.EntryDirection));
                Assert.That(actualItem.Position.Progress, Is.EqualTo(expectedItem.Position.Progress));
                Assert.That(actualItem.Position.Position, Is.EqualTo(expectedItem.Position.Position));
            }

            #endregion
        }
    }
}
