using System;
using Game.BeltSegment;
using NUnit.Framework;
using static Tests.UnitTest.Game.BeltSegment.BeltExternalReplayScenario;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltExternalReplayOrderTest
    {
        [TestCase(0, 2, 1, false), TestCase(0, 2, 1, true)]
        [TestCase(1, 1, 0, false), TestCase(1, 1, 0, true)]
        public void MergeSelectsLinkAtSavedPriorityIndex(int priorityIndex, int expectedItemId, int expectedNextPriority, bool parallel)
        {
            // segmentと逆順で登録。
            // Register opposite to segment order.
            // 保存indexのGUIDを固定。
            // Fix the GUID at the saved index.
            var snapshot = Snapshot(new[]
            {
                BeltReplaySegmentState.Normal(1, 64, new[] { State(1, 0) }),
                BeltReplaySegmentState.Normal(1, 64, new[] { State(2, 0) }),
                BeltReplaySegmentState.Merge(64, priorityIndex, Array.Empty<BeltItemState>(), null)
            }, new[] { new BeltReplayLink(1, 2, BeltDirection.Right), new BeltReplayLink(0, 2, BeltDirection.Front) },
                Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            var graph = new BeltSimulationGraph(snapshot, Array.Empty<IBeltSource>(), Array.Empty<IBeltReceiver>());
            graph.Tick(parallel);

            // 別Graphとの一致でなく、選択したアイテムと次の優先位置を検査する。
            // Assert the selected item and next priority directly without comparing another graph.
            var result = graph.CaptureSnapshot();
            Assert.That(result.Segments[2].Items.Length, Is.EqualTo(1));
            Assert.That(result.Segments[2].Items[0].Item.Guid, Is.EqualTo(State(expectedItemId, 0).Item.Guid));
            Assert.That(result.Segments[2].Items[0].DistanceToExit, Is.EqualTo(192));
            Assert.That(result.Segments[2].PriorityIndex, Is.EqualTo(expectedNextPriority));
            Assert.That(result.Segments[expectedItemId - 1].Items, Is.Empty);
            var remaining = result.Segments[2 - expectedItemId].Items;
            Assert.That(remaining.Length, Is.EqualTo(1));
            Assert.That(remaining[0].Item.Guid, Is.EqualTo(State(3 - expectedItemId, 0).Item.Guid));
            Assert.That(remaining[0].DistanceToExit, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public void BranchSelectsFirstRegisteredExternalOutputWhenBothAccept(bool parallel)
        {
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Branch(1, 64, 0, Array.Empty<BeltItemState>(), State(1, 0).Item) },
                Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(),
                new[] { new BeltReplayOutput(0, BeltDirection.Right), new BeltReplayOutput(0, BeltDirection.Front) });
            var receivers = new[] { new Receiver(), new Receiver() };
            receivers[0].Prepare(true); receivers[1].Prepare(true);
            var graph = new BeltSimulationGraph(snapshot, Array.Empty<IBeltSource>(), receivers);

            graph.Tick(parallel);
            Assert.That(receivers[0].Sent, Is.True);
            Assert.That(receivers[1].Sent, Is.False);
            Assert.That(receivers[0].Item.Guid, Is.EqualTo(State(1, 0).Item.Guid));
            var result = graph.CaptureSnapshot().Segments[0];
            Assert.That(result.BufferedItem, Is.Null);
            Assert.That(result.PriorityIndex, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void MergeReservesFirstRegisteredExternalInputWhenBothReady(bool parallel)
        {
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Merge(64, 0, Array.Empty<BeltItemState>(), null) }, Array.Empty<BeltReplayLink>(),
                new[] { new BeltReplayInput(0, BeltDirection.Left), new BeltReplayInput(0, BeltDirection.Back) }, Array.Empty<BeltReplayOutput>());
            var sources = new[] { new Source(), new Source() };
            sources[0].SetReady(true); sources[1].SetReady(true);
            var graph = new BeltSimulationGraph(snapshot, sources, Array.Empty<IBeltReceiver>());

            graph.Tick(parallel);
            Assert.That(graph.GetInputOffer(0), Is.EqualTo(256));
            Assert.That(graph.GetInputOffer(1), Is.Zero);
            Assert.That(graph.TryInsert(1, 64, State(2, 0).Item), Is.False);
            Assert.That(graph.TryInsert(0, 64, State(1, 0).Item), Is.True);
            Assert.That(graph.CaptureSnapshot().Segments[0].Items[0].Item.Guid, Is.EqualTo(State(1, 0).Item.Guid));
        }

        [TestCase(false), TestCase(true)]
        public void MergePrioritizesInternalLinkOverReadyExternalInput(bool parallel)
        {
            var snapshot = Snapshot(new[]
            {
                BeltReplaySegmentState.Normal(1, 64, new[] { State(1, 0) }),
                BeltReplaySegmentState.Merge(64, 0, Array.Empty<BeltItemState>(), null)
            }, new[] { new BeltReplayLink(0, 1, BeltDirection.Front) },
                new[] { new BeltReplayInput(1, BeltDirection.Left) }, Array.Empty<BeltReplayOutput>());
            var source = new Source();
            source.SetReady(true);
            var graph = new BeltSimulationGraph(snapshot, new[] { source }, Array.Empty<IBeltReceiver>());

            graph.Tick(parallel);
            var result = graph.CaptureSnapshot();
            Assert.That(result.Segments[0].Items, Is.Empty);
            Assert.That(result.Segments[1].Items.Length, Is.EqualTo(1));
            Assert.That(result.Segments[1].Items[0].Item.Guid, Is.EqualTo(State(1, 0).Item.Guid));
            Assert.That(result.Segments[1].PriorityIndex, Is.EqualTo(1));
            Assert.That(graph.GetInputOffer(0), Is.Zero);
            Assert.That(graph.TryInsert(0, 64, State(2, 0).Item), Is.False);
        }
    }
}
