using System;
using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltNetworkTopologyTest;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltRebuildRestorationTest : IBeltExternalReceiverFactory, IBeltItemDropObserver
    {
        private readonly List<(BeltCellItemState Item, string Reason)> dropped = new();
        public IBeltReceiver Create(BeltNetworkConnection connection, int stage) => throw new InvalidOperationException();
        public void OnDropped(BeltCellItemState item, string reason) => dropped.Add((item, reason));

        [TestCase(false)]
        [TestCase(true)]
        public void RemovedMergeEntrySelectsSurvivingDirectionAndHeightTest(bool reverseEdges)
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, -1, 0), Cell(2, -1, 1, 0, 0), Cell(3, 1, 0, 0, 0), Cell(4, 0, 0, 0, 0) };
            var edges = new[] { Edge(1, 4, BeltDirection.Front), Edge(2, 4, BeltDirection.Right), Edge(3, 4, BeltDirection.Left) };
            var item = new BeltCellItemState(4, 128, BeltDirection.Back, 0, Item(1), false);
            network.Rebuild(cells, edges, new[] { item });
            var initial = network.Capture();
            var nextCells = cells.Skip(1).ToArray();
            var nextEdges = reverseEdges ? new[] { edges[2], edges[1] } : new[] { edges[1], edges[2] };
            var change = BeltTopologyChange.Between(initial, nextCells, nextEdges, Array.Empty<BeltCellItemState>());
            var replay = new BeltNetworkReplay(0, initial, this);
            network.Tick();
            network.Rebuild(nextCells, nextEdges, Array.Empty<BeltCellItemState>());
            replay.Apply(new BeltTickDifference(1, Array.Empty<BeltBoundaryChange>(), Array.Empty<BeltOutputResult>(), new BeltBoundaryChange[] { change },
                new BeltTickOrder(1, Array.Empty<uint>(), 1, new uint[] { 2 }, 3)));
            var actual = network.CaptureItems()[0];
            Assert.AreEqual(4, actual.CellId); Assert.AreEqual(128, actual.Progress);
            Assert.AreEqual(BeltDirection.Left, actual.EntryDirection, "Choose the lowest surviving entry direction independently of edge order.");
            Assert.AreEqual(1, actual.EntryHeight);
            CollectionAssert.AreEqual(network.CaptureItems(), replay.Network.CaptureItems());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExitNearestRestorationDoesNotDependOnThirdCandidateTest(bool thirdCellSurvives)
        {
            // 旧横入り経路から正当なcaptureを作り、入口撤去後の候補順を全順列で検査する。
            // Capture valid old side entries and test every candidate order after removing their inputs.
            var old = new BeltTransportNetwork(this, this);
            var oldCells = new[] { Cell(1, 0, 0, 0, 64), Cell(2, 0, 0, 1, 64), Cell(8, -1, 0, 1, 0), Cell(9, -1, 0, 0, 0) };
            var oldEdges = new[] { Edge(9, 1, BeltDirection.Right), Edge(8, 2, BeltDirection.Right), Edge(1, 2, BeltDirection.Front) };
            old.Rebuild(oldCells, oldEdges, new[] { new BeltCellItemState(1, 212, BeltDirection.Left, 0, Item(1), false),
                new BeltCellItemState(9, 128, BeltDirection.Left, 0, Item(2), false), new BeltCellItemState(2, 56, BeltDirection.Left, 0, Item(3), false) });
            var captured = old.CaptureItems();
            Assert.AreEqual(3, captured.Length);
            var cells = thirdCellSurvives ? new[] { oldCells[0], oldCells[1], oldCells[3] } : new[] { oldCells[0], oldCells[1] };
            var edges = new[] { Edge(1, 2, BeltDirection.Front) };
            foreach (int a in new[] { 0, 1, 2 })
            foreach (int b in new[] { 0, 1, 2 })
            {
                if (a == b) continue;
                int c = 3 - a - b;
                dropped.Clear();
                var network = new BeltTransportNetwork(this, this);
                network.Restore(new BeltNetworkSnapshot(cells, edges, new[] { captured[a], captured[b], captured[c] }, Array.Empty<BeltCellPriority>()));
                var items = network.CaptureItems();
                Assert.IsTrue(items.Any(item => item.Item.Guid == Item(3).Guid), $"Exit-nearest C missing for permutation {a},{b},{c}.");
                Assert.IsFalse(items.Any(item => item.Item.Guid == Item(1).Guid));
                Assert.AreEqual(thirdCellSurvives ? 2 : 1, items.Length);
                if (!thirdCellSurvives) Assert.IsTrue(dropped.Any(item => item.Item.Item.Guid == Item(2).Guid && item.Reason.Contains("removed")));
            }
        }

        [TestCase(true, 128)]
        [TestCase(true, 256)]
        [TestCase(false, 128)]
        [TestCase(false, 256)]
        public void DisappearingBufferChecksBodyAcrossSpeedProfileBoundaryTest(bool buffer, int downstreamProgress)
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 64),
                new BeltNetworkCell(2, 0, 0, 1, 32, "other-profile", BeltDirection.Front, new BeltCellSurfaceProfile(0, 0)), Cell(3, 1, 0, 0, 64) };
            var edges = new[] { Edge(1, 2, BeltDirection.Front), Edge(1, 3, BeltDirection.Right) };
            network.Rebuild(cells, edges, new[] { new BeltCellItemState(1, 256, BeltDirection.Back, 0, Item(1), buffer),
                new BeltCellItemState(2, downstreamProgress, BeltDirection.Back, 0, Item(2), false) });
            dropped.Clear();
            network.Rebuild(new[] { cells[0], cells[1] }, new[] { edges[0] }, Array.Empty<BeltCellItemState>());
            var restored = network.CaptureItems();
            bool bufferOverlapsBody = buffer && downstreamProgress < 256;
            Assert.AreEqual(bufferOverlapsBody ? 1 : 2, restored.Length);
            Assert.IsTrue(restored.Any(item => item.Item.Guid == Item(2).Guid));
            Assert.AreEqual(bufferOverlapsBody ? 1 : 0, dropped.Count);
            if (bufferOverlapsBody) Assert.AreEqual(Item(1), dropped[0].Item.Item);
            else Assert.IsFalse(restored.Single(item => item.Item.Guid == Item(1).Guid).IsBuffer);
        }
    }
}
