using System;
using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltNetworkTopologyTest;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltOccupancyChangesTest : IBeltExternalReceiverFactory, IBeltItemDropObserver
    {
        public IBeltReceiver Create(BeltNetworkConnection connection, int stage) => throw new InvalidOperationException();
        public void OnDropped(BeltCellItemState item, string reason) { }

        [Test]
        public void CompressedQueueCrossingsMatchCellOwnershipTest()
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 64), Cell(2, 0, 0, 1, 64), Cell(3, 0, 0, 2, 64),
                Cell(4, 0, 0, 3, 64), Cell(5, 0, 0, 4, 64), Cell(6, 0, 0, 5, 64) };
            var edges = new[] { Edge(1, 2, BeltDirection.Front), Edge(2, 3, BeltDirection.Front),
                Edge(3, 4, BeltDirection.Front), Edge(4, 5, BeltDirection.Front), Edge(5, 6, BeltDirection.Front) };
            network.Rebuild(cells, edges, new[] { State(6, 207, 1, false), State(4, 83, 2, false), State(1, 151, 3, false) });
            var expected = new Dictionary<int, int>();
            AssertOwnership();
            for (int tick = 0; tick < 32; tick++) { network.Tick(); AssertOwnership(); }
            Assert.AreEqual(3, network.CaptureItems().Length);
            Assert.AreEqual(0, network.DrainOccupancyChanges().Count, "A blocked queue must not emit changes.");
            network.SetSpeeds(new[] { new BeltCellSpeed(4, 0) });
            Assert.AreEqual(0, network.DrainOccupancyChanges().Count, "A speed split preserves occupancy.");
            network.SetSpeeds(new[] { new BeltCellSpeed(4, 64) });
            Assert.AreEqual(0, network.DrainOccupancyChanges().Count, "Rejoining a path preserves occupancy.");

            #region Internal
            void AssertOwnership()
            {
                // 検証側だけで全状態を数え、物理操作からの差分と照合する。
                // Count full state only in the test and compare it with operation-derived changes.
                foreach (var change in network.DrainOccupancyChanges())
                {
                    expected.TryGetValue(change.Key, out int previous);
                    int count = previous + change.Value;
                    Assert.That(count, Is.GreaterThanOrEqualTo(0));
                    if (count == 0) expected.Remove(change.Key);
                    else expected[change.Key] = count;
                }
                var actual = new Dictionary<int, int>();
                foreach (var item in network.CaptureItems())
                    actual[item.CellId] = actual.TryGetValue(item.CellId, out int count) ? count + 1 : 1;
                CollectionAssert.AreEquivalent(actual, expected);
            }
            #endregion
        }

        [Test]
        public void SameCellBufferAndReplacementCancelBeforePublicationTest()
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 0), Cell(2, 0, 0, 1, 0), Cell(3, 1, 0, 0, 0) };
            var edges = new[] { Edge(1, 2, BeltDirection.Front), Edge(1, 3, BeltDirection.Right) };
            network.Rebuild(cells, edges, new[] { State(1, 256, 1, false) });
            Assert.AreEqual(1, network.DrainOccupancyChanges()[1]);
            network.Tick();
            Assert.IsTrue(network.CaptureItems()[0].IsBuffer);
            Assert.AreEqual(0, network.DrainOccupancyChanges().Count, "Queue to buffer keeps the same owner.");
            network.ReplaceCellItems(1, new[] { State(1, 256, 2, true) });
            Assert.AreEqual(Item(2), network.CaptureItems()[0].Item);
            Assert.AreEqual(0, network.DrainOccupancyChanges().Count, "Identity replacement keeps the same count.");
            network.Restore(network.Capture());
            Assert.AreEqual(0, network.DrainOccupancyChanges().Count, "Restore cancels old and new ownership.");
            network.Rebuild(cells, new[] { Edge(1, 2, BeltDirection.Front) }, Array.Empty<BeltCellItemState>());
            Assert.IsFalse(network.CaptureItems()[0].IsBuffer);
            Assert.AreEqual(0, network.DrainOccupancyChanges().Count, "Disappearing buffer remains in its empty cell.");
            network.Rebuild(new[] { cells[1], cells[2] }, Array.Empty<BeltNetworkConnection>(), Array.Empty<BeltCellItemState>());
            Assert.AreEqual(-1, network.DrainOccupancyChanges()[1]);
            Assert.AreEqual(0, network.CaptureItems().Length);
        }

        [Test]
        public void LoopOutputAndDeferredInputCancelPerCellTest()
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 64), Cell(2, 0, 0, 1, 64), Cell(3, 1, 0, 1, 64), Cell(4, 1, 0, 0, 64) };
            var edges = new[] { Edge(1, 2, BeltDirection.Front), Edge(2, 3, BeltDirection.Right),
                Edge(3, 4, BeltDirection.Back), Edge(4, 1, BeltDirection.Left) };
            network.Rebuild(cells, edges, Array.Empty<BeltCellItemState>());
            Assert.IsTrue(network.TryInsert(1, BeltDirection.Back, 256, Item(1)));
            Assert.AreEqual(1, network.DrainOccupancyChanges()[1]);
            int previous = 1;
            // 一周後も送出・遅延搬入の差分が二重計上されない。
            // Output and deferred input are counted once across repeated loop wraps.
            for (int tick = 0; tick < 64; tick++)
            {
                network.Tick();
                int current = network.CaptureItems()[0].CellId;
                var changes = network.DrainOccupancyChanges();
                Assert.AreEqual(previous == current ? 0 : 2, changes.Count);
                if (previous != current) { Assert.AreEqual(-1, changes[previous]); Assert.AreEqual(1, changes[current]); }
                previous = current;
            }
        }

        [Test]
        public void MergeBranchAndBufferTransfersPushExactOwnershipTest()
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 64), Cell(2, 2, 0, 0, 32), Cell(3, 1, 0, 1, 64),
                Cell(4, 1, 0, 2, 128), Cell(5, 0, 0, 2, 64), Cell(6, 2, 0, 2, 32) };
            var edges = new[] { Edge(1, 3, BeltDirection.Right), Edge(2, 3, BeltDirection.Left), Edge(3, 4, BeltDirection.Front),
                Edge(4, 5, BeltDirection.Left), Edge(4, 6, BeltDirection.Right), Edge(5, 1, BeltDirection.Back), Edge(6, 2, BeltDirection.Back) };
            network.Rebuild(cells, edges, new[] { State(1, 256, 1, false), State(2, 256, 2, false), State(5, 128, 3, false), State(6, 256, 4, false) });
            var counts = new int[7];
            // 合流予約と段階3・4を跨ぐ所有数を毎回照合する。
            // Check ownership through merge reservations and stage-three/four transfers.
            for (int tick = 0; tick < 200; tick++)
            {
                foreach (var change in network.DrainOccupancyChanges()) counts[change.Key] += change.Value;
                var snapshot = network.CaptureItems();
                Assert.AreEqual(4, snapshot.Length);
                foreach (var cell in cells) Assert.AreEqual(snapshot.Count(item => item.CellId == cell.Id), counts[cell.Id], $"tick={tick}, cell={cell.Id}");
                network.Tick();
            }
        }

        private static BeltCellItemState State(int cell, int progress, int id, bool buffer) =>
            new BeltCellItemState(cell, progress, BeltDirection.Back, 0, Item(id), buffer);
    }
}
