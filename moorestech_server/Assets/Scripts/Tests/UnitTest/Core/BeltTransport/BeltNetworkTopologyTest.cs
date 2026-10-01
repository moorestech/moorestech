using System;
using System.Collections.Generic;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltNetworkTopologyTest : IBeltExternalReceiverFactory, IBeltItemDropObserver
    {
        private readonly List<BeltCellItemState> dropped = new List<BeltCellItemState>();
        public IBeltReceiver Create(BeltNetworkConnection connection, int stage) => throw new InvalidOperationException("No external receiver in this fixture.");
        public void OnDropped(BeltCellItemState item, string reason) => dropped.Add(item);

        [Test]
        public void EqualSpeedCurvesAndSlopesCoalesceTest()
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 64), Cell(2, 0, 1, 1, 64), Cell(3, 1, 1, 1, 64), Cell(4, 2, 1, 1, 32) };
            var edges = new[] { Edge(1, 2, BeltDirection.Front), Edge(2, 3, BeltDirection.Right), Edge(3, 4, BeltDirection.Right) };
            network.Rebuild(cells, edges, Array.Empty<BeltCellItemState>());
            Assert.AreSame(network.GetPath(1), network.GetPath(2));
            Assert.AreEqual(3, network.GetPath(1).Cells.Length);
            Assert.AreSame(network.GetPath(1), network.GetPath(3));
            Assert.AreNotSame(network.GetPath(3), network.GetPath(4));
        }

        [Test]
        public void LoopCutUsesCoordinatesInsteadOfRegistrationOrderTest()
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 1, 0, 1, 64), Cell(2, 0, 0, 1, 64), Cell(3, 0, 0, 0, 64), Cell(4, 1, 0, 0, 64) };
            network.Rebuild(cells, new[] { Edge(1, 2, BeltDirection.Left), Edge(2, 3, BeltDirection.Back),
                Edge(3, 4, BeltDirection.Right), Edge(4, 1, BeltDirection.Front) }, Array.Empty<BeltCellItemState>());
            foreach (var cell in cells) Assert.AreSame(network.GetPath(3), network.GetPath(cell.Id));
            Assert.AreEqual(3, network.GetPath(3).Cells[0].Id);
            Assert.AreSame(network.GetPath(3).Segment, network.GetPath(3).Segment.Output);
        }

        [Test]
        public void EntryMatchingItemWinsOverlapAfterMergeRemovalTest()
        {
            dropped.Clear();
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 64), Cell(2, 0, 0, 1, 64) };
            // 経路一致の後続を優先し、位置が近い横入り候補を落とす。
            // Favor the matching follower over the overlapping side-entry candidate.
            var items = new[] { new BeltCellItemState(2, 64, BeltDirection.Left, 0, Item(1), false),
                new BeltCellItemState(1, 128, BeltDirection.Back, 0, Item(2), false) };
            network.Rebuild(cells, new[] { Edge(1, 2, BeltDirection.Front) }, items);
            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(Item(1), dropped[0].Item);
            Assert.AreEqual(Item(2), network.CaptureItems()[0].Item);
        }

        [Test]
        public void DisappearingBufferRequiresEntireOriginalCellTest()
        {
            dropped.Clear();
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 64), Cell(2, 0, 0, 1, 64) };
            var items = new[] { new BeltCellItemState(2, 64, BeltDirection.Back, 0, Item(1), false),
                new BeltCellItemState(1, 256, BeltDirection.Back, 0, Item(2), true) };
            network.Rebuild(cells, new[] { Edge(1, 2, BeltDirection.Front) }, items);
            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(Item(2), dropped[0].Item);
        }

        [Test]
        public void RestoredMergeEntrySurvivesPriorityResetAndUnrelatedReservationsTest()
        {
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 0), Cell(2, -1, 1, 1, 0), Cell(3, 0, 0, 1, 0) };
            var edges = new[] { Edge(1, 3, BeltDirection.Front), Edge(2, 3, BeltDirection.Right) };
            var item = new BeltCellItemState(3, 64, BeltDirection.Left, 1, Item(1), false);
            network.Restore(new BeltNetworkSnapshot(cells, edges, new[] { item }, new[] { new BeltCellPriority(3, 57) }));
            network.Tick();
            Assert.AreEqual(BeltDirection.Left, network.CaptureItems()[0].EntryDirection);
            Assert.AreEqual(1, network.CaptureItems()[0].EntryHeight);
            network.Rebuild(cells, edges, Array.Empty<BeltCellItemState>());
            network.Tick();
            Assert.AreEqual(BeltDirection.Left, network.CaptureItems()[0].EntryDirection);
            Assert.AreEqual(1, network.CaptureItems()[0].EntryHeight);
        }

        [Test]
        public void AtomicSpeedRepartitionPreservesItemsAndBranchPriorityTest()
        {
            dropped.Clear();
            var network = new BeltTransportNetwork(this, this);
            var cells = new[] { Cell(1, 0, 0, 0, 0), Cell(2, 0, 0, 1, 0), Cell(3, 0, 0, 2, 0),
                Cell(4, 0, 0, 3, 0), Cell(5, 1, 0, 2, 0) };
            var edges = new[] { Edge(1, 2, BeltDirection.Front), Edge(2, 3, BeltDirection.Front),
                Edge(3, 4, BeltDirection.Front), Edge(3, 5, BeltDirection.Right) };
            var items = new[] { new BeltCellItemState(1, 256, BeltDirection.Back, 0, Item(1), false),
                new BeltCellItemState(2, 256, BeltDirection.Back, 0, Item(2), false),
                new BeltCellItemState(3, 256, BeltDirection.Back, 0, Item(3), false),
                new BeltCellItemState(3, 256, BeltDirection.Back, 0, Item(4), true) };
            network.Restore(new BeltNetworkSnapshot(cells, edges, items, new[] { new BeltCellPriority(3, 57) }));
            network.SetSpeeds(new[] { new BeltCellSpeed(1, 64), new BeltCellSpeed(2, 32), new BeltCellSpeed(3, 64) });
            Assert.AreNotSame(network.GetPath(1), network.GetPath(2));
            Assert.AreEqual(57, network.GetPath(3).Segment.PriorityOrder);
            network.SetSpeeds(new[] { new BeltCellSpeed(2, 64) });
            Assert.AreSame(network.GetPath(1), network.GetPath(3));
            Assert.AreEqual(57, network.GetPath(3).Segment.PriorityOrder);
            CollectionAssert.AreEquivalent(items, network.CaptureItems());
            Assert.AreEqual(0, dropped.Count);
        }

        internal static BeltNetworkCell Cell(int id, int x, int y, int z, int speed) =>
            new BeltNetworkCell(id, x, y, z, speed, "same-profile", BeltDirection.Front, new BeltCellSurfaceProfile(0, 0));
        internal static BeltNetworkConnection Edge(int source, int target, BeltDirection direction) =>
            new BeltNetworkConnection(source, target, true, true, direction, 0);
    }
}
