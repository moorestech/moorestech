using System;
using Game.BeltSegment;
using NUnit.Framework;
using static Tests.UnitTest.Game.BeltSegment.BeltExternalReplayScenario;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltExternalReplayTest
    {
        [TestCase(false), TestCase(true)]
        public void CompleteExternalOutcomesReplayFor1000Ticks(bool parallel) => Run(parallel);

        [TestCase(false), TestCase(true)]
        public void EmptyGraphSurvivesTickAndReconstruction(bool parallel)
        {
            var snapshot = Snapshot(Array.Empty<BeltReplaySegmentState>(), Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            var replay = new BeltReplaySimulation(snapshot);
            replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>()), parallel);
            AssertSame(snapshot, new BeltReplaySimulation(replay.CaptureSnapshot()).CaptureSnapshot());
        }

        [TestCase(64, 448, 384), TestCase(1, 511, 447)]
        public void AfterTickInsertionDoesNotAdvanceUntilNextTick(int length, int insertedDistance, int nextDistance)
        {
            var item = State(1, 0).Item;
            var snapshot = Single(BeltReplaySegmentState.Normal(2, 64, Array.Empty<BeltItemState>()), true, false);
            var replay = new BeltReplaySimulation(snapshot);
            replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>(), new BeltReplayInsertion(0, length, item)), false);
            Assert.That(replay.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.EqualTo(insertedDistance));
            replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>()), false);
            var state = replay.CaptureSnapshot().Segments[0].Items[0];
            Assert.That(state.DistanceToExit, Is.EqualTo(nextDistance));
            Assert.That(state.Item.Guid, Is.EqualTo(item.Guid));
            Assert.That(state.Item.Position, Is.SameAs(item.Position));
        }

        [Test]
        public void OutputSuccessResetsAndDuplicateNotificationsAreIdempotent()
        {
            var item = State(1, 0);
            var replay = new BeltReplaySimulation(Single(BeltReplaySegmentState.Normal(1, 64, new[] { item }), true, true));
            replay.ApplyTick(Frame(new[] { 0, 0 }, new[] { 0, 0 }, new BeltReplayInsertion(0, 256, item.Item)), false);
            replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>()), false);
            Assert.That(replay.CaptureSnapshot().Segments[0].Items, Is.EqualTo(new[] { item }));
        }

        [Test]
        public void MergeReadinessSwitchesAndThenResets()
        {
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Merge(64, 0, Array.Empty<BeltItemState>(), null) }, Array.Empty<BeltReplayLink>(),
                new[] { new BeltReplayInput(0, BeltDirection.Back), new BeltReplayInput(0, BeltDirection.Left) }, Array.Empty<BeltReplayOutput>());
            var sources = new[] { new Source(), new Source() };
            var actual = new BeltSimulationGraph(snapshot, sources, Array.Empty<IBeltReceiver>());
            var replay = new BeltReplaySimulation(snapshot);
            // 実Coreの予約切替と、再現ポートの毎tickリセットを照合する。
            // Compare real Core reservation rotation with per-tick replay port resets.
            for (int i = 0; i < 2; i++)
            {
                sources[0].SetReady(i == 0); sources[1].SetReady(i == 1);
                actual.Simulation.Tick(false);
                Assert.That(actual.Segments[0].TryReceive(snapshot.Inputs[i].InputDirection, 256, State(i + 1, 0).Item), Is.True);
                replay.ApplyTick(Frame(new[] { i }, Array.Empty<int>(), new BeltReplayInsertion(i, 256, State(i + 1, 0).Item)), true);
                AssertSame(actual.CaptureSnapshot(), replay.CaptureSnapshot());
            }
            var reset = new BeltReplaySimulation(snapshot);
            reset.ApplyTick(Frame(new[] { 0 }, Array.Empty<int>()), false);
            Assert.Throws<InvalidOperationException>(() => reset.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>(), new BeltReplayInsertion(0, 256, State(1, 0).Item)), false));
        }

        [Test]
        public void BranchRetainsRejectedItemThenRotatesFromStartIndex()
        {
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Branch(1, 64, 0, Array.Empty<BeltItemState>(), State(1, 0).Item) },
                Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(), new[]
                { new BeltReplayOutput(0, BeltDirection.Front), new BeltReplayOutput(0, BeltDirection.Right), new BeltReplayOutput(0, BeltDirection.Left) });
            var replay = new BeltReplaySimulation(snapshot);
            replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>()), false);
            AssertSame(snapshot, replay.CaptureSnapshot());
            replay.ApplyTick(Frame(Array.Empty<int>(), new[] { 1 }), false);
            var state = replay.CaptureSnapshot().Segments[0];
            Assert.That(state.BufferedItem, Is.Null);
            Assert.That(state.PriorityIndex, Is.EqualTo(1));
        }

        [Test]
        public void BranchRegistersInternalOutputFirstAndPreservesPriorityAfterRestore()
        {
            var snapshot = Snapshot(new[]
            {
                BeltReplaySegmentState.Branch(1, 64, 0, new[] { State(2, 0) }, State(1, 0).Item),
                BeltReplaySegmentState.Normal(2, 64, Array.Empty<BeltItemState>())
            }, new[] { new BeltReplayLink(0, 1, BeltDirection.Front) }, Array.Empty<BeltReplayInput>(),
                new[] { new BeltReplayOutput(0, BeltDirection.Right) });
            var replay = new BeltReplaySimulation(snapshot);
            replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>()), false);
            var first = replay.CaptureSnapshot();
            // 内部出力を先に選び、bufferから通常列への搬入は同tickに前進する。
            // Select the internal output first and advance its buffer insertion in the same tick.
            Assert.That(first.Segments[0].PriorityIndex, Is.EqualTo(1));
            Assert.That(first.Segments[1].Items[0].DistanceToExit, Is.EqualTo(384));
            Assert.That(first.Segments[1].Items[0].Item.Guid, Is.EqualTo(State(1, 0).Item.Guid));
            var restored = new BeltReplaySimulation(first);
            var next = Frame(Array.Empty<int>(), new[] { 0 });
            replay.ApplyTick(next, false); restored.ApplyTick(next, true);
            AssertSame(replay.CaptureSnapshot(), restored.CaptureSnapshot());
            Assert.That(restored.CaptureSnapshot().Segments[0].PriorityIndex, Is.Zero);
            Assert.That(restored.CaptureSnapshot().Segments[0].BufferedItem, Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public void NormalSelfLinkMovesOnceAndFullSelfLinkStops(bool full)
        {
            var items = full ? new[] { State(1, 0), State(2, 256) } : new[] { State(1, 32) };
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Normal(full ? 2 : 4, 64, items) },
                new[] { new BeltReplayLink(0, 0, BeltDirection.Front) }, Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            var replay = new BeltReplaySimulation(snapshot);
            replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>()), true);
            if (full) AssertSame(snapshot, replay.CaptureSnapshot());
            else Assert.That(replay.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.EqualTo(992));
        }

        [Test]
        public void SnapshotArraysAreIsolatedFromGraphAndReplay()
        {
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Normal(2, 64, new[] { State(1, 256) }), BeltReplaySegmentState.Normal(2, 64, Array.Empty<BeltItemState>()) },
                new[] { new BeltReplayLink(0, 1, BeltDirection.Front) }, new[] { new BeltReplayInput(0, BeltDirection.Back) }, new[] { new BeltReplayOutput(1, BeltDirection.Front) });
            var replay = new BeltReplaySimulation(snapshot);
            var expected = new BeltReplaySimulation(snapshot);
            var captured = replay.CaptureSnapshot();
            // 入力とCapture双方の配列要素を壊しても実行中の所有状態は変わらない。
            // Mutating both input and captured array entries must not alter live ownership.
            Corrupt(snapshot); Corrupt(captured);
            var frame = Frame(new[] { 0 }, Array.Empty<int>(), new BeltReplayInsertion(0, 64, State(2, 0).Item));
            expected.ApplyTick(frame, false); replay.ApplyTick(frame, true);
            AssertSame(expected.CaptureSnapshot(), replay.CaptureSnapshot());
            #region Internal
            void Corrupt(BeltReplaySnapshot value)
            {
                value.Segments[0].Items[0] = State(99, 0);
                value.Segments[1] = BeltReplaySegmentState.Normal(1, 0, Array.Empty<BeltItemState>());
                value.Links[0] = new BeltReplayLink(1, 0, BeltDirection.Left);
                value.Inputs[0] = new BeltReplayInput(1, BeltDirection.Left);
                value.Outputs[0] = new BeltReplayOutput(0, BeltDirection.Left);
            }
            #endregion
        }

        [Test]
        public void KindFactoriesAndExternalPortCountsKeepTheirContracts()
        {
            var normal = BeltReplaySegmentState.Normal(2, 64, Array.Empty<BeltItemState>());
            var merge = BeltReplaySegmentState.Merge(64, 2, Array.Empty<BeltItemState>(), State(1, 0).Item);
            var branch = BeltReplaySegmentState.Branch(3, 64, 1, Array.Empty<BeltItemState>(), State(2, 0).Item);
            Assert.That((normal.Kind, normal.Capacity, normal.PriorityIndex, normal.BufferedItem), Is.EqualTo((BeltSegmentKind.Normal, 2, 0, (BeltItem?)null)));
            Assert.That((merge.Kind, merge.Capacity, merge.PriorityIndex), Is.EqualTo((BeltSegmentKind.Merge, 1, 2)));
            Assert.That((branch.Kind, branch.Capacity, branch.PriorityIndex), Is.EqualTo((BeltSegmentKind.Branch, 3, 1)));
            var snapshot = Single(normal, true, true);
            Assert.Throws<ArgumentException>(() => new BeltSimulationGraph(snapshot, Array.Empty<IBeltSource>(), new[] { new Receiver() }));
            Assert.Throws<ArgumentException>(() => new BeltSimulationGraph(snapshot, new[] { new Source() }, Array.Empty<IBeltReceiver>()));
        }

        [TestCase(false), TestCase(true)]
        public void UnexecutedRecordedOutputThrows(bool stoppedBranch)
        {
            var state = stoppedBranch ? BeltReplaySegmentState.Branch(1, 0, 0, Array.Empty<BeltItemState>(), State(1, 0).Item)
                : BeltReplaySegmentState.Normal(1, 64, Array.Empty<BeltItemState>());
            var replay = new BeltReplaySimulation(Single(state, false, true));
            Assert.Throws<InvalidOperationException>(() => replay.ApplyTick(Frame(Array.Empty<int>(), new[] { 0 }), false));
        }

        [TestCase(0), TestCase(257)]
        public void InvalidInsertionLengthThrows(int length)
        {
            var replay = new BeltReplaySimulation(Single(BeltReplaySegmentState.Normal(1, 64, Array.Empty<BeltItemState>()), true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>(), new BeltReplayInsertion(0, length, State(1, 0).Item)), false));
        }

        [Test]
        public void RecordedInsertionIntoFullQueueThrows()
        {
            var replay = new BeltReplaySimulation(Single(BeltReplaySegmentState.Normal(1, 64, new[] { State(1, 0) }), true, false));
            Assert.Throws<InvalidOperationException>(() => replay.ApplyTick(Frame(Array.Empty<int>(), Array.Empty<int>(), new BeltReplayInsertion(0, 1, State(2, 0).Item)), false));
        }

        static BeltReplaySnapshot Single(BeltReplaySegmentState state, bool input, bool output)
            => Snapshot(new[] { state }, Array.Empty<BeltReplayLink>(), input ? new[] { new BeltReplayInput(0, BeltDirection.Back) } : Array.Empty<BeltReplayInput>(),
                output ? new[] { new BeltReplayOutput(0, BeltDirection.Front) } : Array.Empty<BeltReplayOutput>());
    }
}
