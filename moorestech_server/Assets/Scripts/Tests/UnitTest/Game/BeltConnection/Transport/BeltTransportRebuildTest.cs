using Core.BeltTransport;
using Core.Master;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Transport
{
    // 再構築で走行中アイテムが所属マスとマス内進行量を保って復元されるか(撤去と再構築の仕様)
    // Running items are restored on rebuild keeping their cell and in-cell progress (removal/rebuild spec)
    public class BeltTransportRebuildTest
    {
        private static readonly ItemId ItemA = new(1);

        [Test]
        public void RemovingLastCellDeletesItsItemAndKeepsOthersInCell()
        {
            var world = StraightBelts(0, 4);
            var old = Assemble(world);

            // 4マス(長さ1024)へ z=3・z=1・z=0 のマスに先頭がある3個を置く
            // Put three items whose heads are on cells z=3, z=1 and z=0 of the 4-cell (length 1024) segment
            var onZ3 = NewItem(ItemA, BeltEntryDirection.FromBack);
            var onZ1 = NewItem(ItemA, BeltEntryDirection.FromBack);
            var onZ0 = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, Vector3Int.zero).RestoreItems(new[] { new BeltItemState(onZ3, 100), new BeltItemState(onZ1, 600), new BeltItemState(onZ0, 900) });

            // z=3を撤去すると、z=1はマス内残り88で出口から256+88、z=0は残り132で512+132。z=3のアイテムは消える
            // Removing z=3: z=1 keeps 88 inside its cell at 256+88, z=0 keeps 132 at 512+132; the z=3 item vanishes
            world.RemoveBlock(new Vector3Int(0, 0, 3), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            AssertRun(SegmentAt(next, Vector3Int.zero), (onZ1, 344), (onZ0, 644));
        }

        [Test]
        public void RemovingMiddleCellSplitsItemsIntoBothSides()
        {
            var world = StraightBelts(0, 4);
            var old = Assemble(world);
            var onZ3 = NewItem(ItemA, BeltEntryDirection.FromBack);
            var onZ1 = NewItem(ItemA, BeltEntryDirection.FromBack);
            var onZ0 = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, Vector3Int.zero).RestoreItems(new[] { new BeltItemState(onZ3, 100), new BeltItemState(onZ1, 600), new BeltItemState(onZ0, 900) });

            // z=1を撤去すると z=0 単独と z=2..3 に分かれる。z=1のアイテムだけ消え、他はマス内進行量を保つ
            // Removing z=1 splits into z=0 alone and z=2..3; only the z=1 item vanishes and the others keep their in-cell progress
            world.RemoveBlock(new Vector3Int(0, 0, 1), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            AssertRun(SegmentAt(next, new Vector3Int(0, 0, 3)), (onZ3, 100));
            AssertRun(SegmentAt(next, Vector3Int.zero), (onZ0, 132));
        }

        [Test]
        public void ExtendingBothEndsKeepsCellAndProgress()
        {
            var world = StraightBelts(0, 2);
            var old = Assemble(world);
            var onZ1 = NewItem(ItemA, BeltEntryDirection.FromBack);
            var onZ0 = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, Vector3Int.zero).RestoreItems(new[] { new BeltItemState(onZ1, 50), new BeltItemState(onZ0, 400) });

            // 前後に1マスずつ足して z=-1..2 の1本になる。z=1は出口から256+50、z=0は512+144
            // Adding one cell at each end makes one z=-1..2 segment; z=1 sits at 256+50 and z=0 at 512+144
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, -1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var next = Rebuild(old, world);
            Assert.AreEqual(1, next.Segments.Length);
            AssertRun(next.Segments[0], (onZ1, 306), (onZ0, 656));
        }

        [Test]
        public void MergeDissolutionDeletesBSideItemOverlappingRestoredASideItem()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var fromB = NewItem(ItemA, BeltEntryDirection.FromRight);
            var onA = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, new Vector3Int(0, 0, 1)).RestoreItems(new[] { new BeltItemState(fromB, 200) });
            SegmentAt(old, Vector3Int.zero).RestoreItems(new[] { new BeltItemState(onA, 10) });

            // Bを撤去すると z=-1..2 の1本になる。A側(優先1)は512+10、B由来(優先2)は256+200で重なるので消える
            // Removing B leaves one z=-1..2 segment; the A-side item (priority 1) lands at 512+10 and the overlapping B item (priority 2) at 256+200 vanishes
            world.RemoveBlock(new Vector3Int(1, 0, 1), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            Assert.AreEqual(1, next.Segments.Length);
            AssertRun(next.Segments[0], (onA, 522));
        }

        [Test]
        public void MergeDissolutionKeepsFreeBSideItemWithAlignedEntry()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var fromB = NewItem(ItemA, BeltEntryDirection.FromRight);
            var onA = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, new Vector3Int(0, 0, 1)).RestoreItems(new[] { new BeltItemState(fromB, 200) });
            SegmentAt(old, Vector3Int.zero).RestoreItems(new[] { new BeltItemState(onA, 230) });

            // A側は512+230=742で、B由来の456とは286離れて重ならない。B由来は進入方向を直進(FromBack)へ合わせて残る
            // The A-side item at 512+230=742 is 286 away from the B item at 456; the B item stays with its entry aligned to straight (FromBack)
            world.RemoveBlock(new Vector3Int(1, 0, 1), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            AssertRun(next.Segments[0], (fromB.WithEntryDirection(BeltEntryDirection.FromBack), 456), (onA, 742));
        }

        [Test]
        public void SamePriorityOverlapKeepsItemNearerToNewExit()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var inMerge = NewItem(ItemA, BeltEntryDirection.FromBack);
            var onA = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, new Vector3Int(0, 0, 1)).RestoreItems(new[] { new BeltItemState(inMerge, 200) });
            SegmentAt(old, Vector3Int.zero).RestoreItems(new[] { new BeltItemState(onA, 10) });

            // どちらも優先1。同じ新segmentで重なるので、出口に近い合流マスの456が残りA側の522は消える
            // Both are priority 1 and overlap in one new segment, so the merge-cell item at 456 nearer the exit stays and the A item at 522 vanishes
            world.RemoveBlock(new Vector3Int(1, 0, 1), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            AssertRun(next.Segments[0], (inMerge, 456));
        }

        [Test]
        public void OverlappingBodiesInDifferentNewSegmentsAreBothKept()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var inMerge = NewItem(ItemA, BeltEntryDirection.FromBack);
            var onA = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, new Vector3Int(0, 0, 1)).RestoreItems(new[] { new BeltItemState(inMerge, 200) });
            SegmentAt(old, Vector3Int.zero).RestoreItems(new[] { new BeltItemState(onA, 10) });

            // Cを1マス延ばすだけなら合流は残る。合流の胴体がAへはみ出していても、別segment同士なので両方残る
            // Only extending C keeps the merge; the merge item's body sticks into A, but different segments never conflict so both stay
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 3), BlockDirection.North);
            var next = Rebuild(old, world);
            AssertRun(SegmentAt(next, new Vector3Int(0, 0, 1)), (inMerge, 200));
            AssertRun(SegmentAt(next, Vector3Int.zero), (onA, 10));
        }

        #region Worlds

        private static IWorldBlockDatastore StraightBelts(int fromZ, int count)
        {
            var world = NewWorld();
            for (var z = fromZ; z < fromZ + count; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            return world;
        }

        // A(z=-1..0)とB(x=1から西向き)が合流M(z=1)に入り、C(z=2)へ流れる
        // A (z=-1..0) and B (westward from x=1) enter merge M (z=1), which flows to C (z=2)
        private static IWorldBlockDatastore MergeWorld()
        {
            var world = StraightBelts(-1, 4);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            return world;
        }

        #endregion
    }
}
