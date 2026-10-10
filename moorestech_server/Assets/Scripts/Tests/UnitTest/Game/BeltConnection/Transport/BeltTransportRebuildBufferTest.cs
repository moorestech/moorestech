using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Transport
{
    // 再構築で合流・分岐のbufferと内部segmentのアイテムがどう引き継がれるか(撤去と再構築の仕様)
    // How items in merge/branch buffers and internal segments are carried over on rebuild (removal/rebuild spec)
    public class BeltTransportRebuildBufferTest
    {
        private static readonly ItemId ItemA = new(1);
        private static readonly Vector3Int MergeCell = new(0, 0, 1);

        [Test]
        public void SurvivingMergeBufferKeepsItsItem()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var held = NewItem(ItemA, BeltEntryDirection.FromRight);
            ((BeltBufferedSegment)SegmentAt(old, MergeCell)).Buffer.RestoreItem(held);

            // Cを延ばしても合流は残るので、bufferのアイテムは進入方向も含めてそのまま引き継ぐ
            // Extending C keeps the merge, so the buffer item is carried over as is, entry direction included
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 3), BlockDirection.North);
            var next = Rebuild(old, world);
            var merge = (BeltBufferedSegment)SegmentAt(next, MergeCell);
            Assert.IsTrue(merge.Buffer.TryGetItem(out var restored));
            AssertSameItem(held, restored);
            AssertRun(merge);
        }

        [Test]
        public void VanishingMergeBufferItemIsPlacedAtCellExitWhenCellIsFree()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var held = NewItem(ItemA, BeltEntryDirection.FromRight);
            var onC = NewItem(ItemA, BeltEntryDirection.FromBack);
            ((BeltBufferedSegment)SegmentAt(old, MergeCell)).Buffer.RestoreItem(held);
            SegmentAt(old, new Vector3Int(0, 0, 2)).RestoreItems(new[] { new BeltItemState(onC, 0) });

            // Bを撤去すると z=-1..2 の1本になり合流のbufferは消える。Cの出口で止まるアイテムの後端は256ちょうどでMのマスは空いている
            // Removing B leaves one z=-1..2 segment and the merge buffer vanishes; C's item parked at the exit ends exactly at 256, leaving M's cell free
            world.RemoveBlock(new Vector3Int(1, 0, 1), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);

            // 先頭をMのマスの出口(256)に置き、進入方向は新しい経路の直進(FromBack)へ合わせる
            // Its head goes to M's cell exit (256) and its entry direction is aligned to the new straight path (FromBack)
            Assert.AreEqual(1, next.Segments.Length);
            AssertRun(next.Segments[0], (onC, 0), (held.WithEntryDirection(BeltEntryDirection.FromBack), 256));
        }

        [Test]
        public void VanishingMergeBufferItemIsDeletedWhenRunningItemOccupiesCell()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var held = NewItem(ItemA, BeltEntryDirection.FromRight);
            var running = NewItem(ItemA, BeltEntryDirection.FromBack);
            var merge = (BeltBufferedSegment)SegmentAt(old, MergeCell);
            merge.Buffer.RestoreItem(held);
            merge.RestoreItems(new[] { new BeltItemState(running, 100) });

            // 走行中アイテムを先に256+100へ置くので、Mのマスは空いておらずbufferのアイテムは消える
            // The running item is placed first at 256+100, so M's cell is not free and the buffer item vanishes
            world.RemoveBlock(new Vector3Int(1, 0, 1), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            AssertRun(next.Segments[0], (running, 356));
        }

        [Test]
        public void VanishingMergeBufferItemIsDeletedWhenDownstreamBodyOccupiesCell()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            var held = NewItem(ItemA, BeltEntryDirection.FromRight);
            var onC = NewItem(ItemA, BeltEntryDirection.FromBack);
            ((BeltBufferedSegment)SegmentAt(old, MergeCell)).Buffer.RestoreItem(held);
            SegmentAt(old, new Vector3Int(0, 0, 2)).RestoreItems(new[] { new BeltItemState(onC, 10) });

            // Cのアイテムは出口から10、後端266でMのマスへ10だけかかるので、bufferのアイテムは消える
            // C's item sits 10 from the exit and ends at 266, reaching 10 into M's cell, so the buffer item vanishes
            world.RemoveBlock(new Vector3Int(1, 0, 1), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            AssertRun(next.Segments[0], (onC, 10));
        }

        [Test]
        public void MergeBufferItemIsDeletedWhenItsCellIsRemoved()
        {
            var world = MergeWorld();
            var old = Assemble(world);
            ((BeltBufferedSegment)SegmentAt(old, MergeCell)).Buffer.RestoreItem(NewItem(ItemA, BeltEntryDirection.FromRight));

            world.RemoveBlock(MergeCell, BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            Assert.AreEqual(0, ItemCount(next));
        }

        [Test]
        public void InternalSegmentItemIsCarriedOverOnUnrelatedChange()
        {
            var world = MachineMergeWorld();
            var old = Assemble(world);
            var item = NewItem(ItemA, BeltEntryDirection.FromLeft);
            old.Segments[InternalSegmentIndex(old)].RestoreItems(new[] { new BeltItemState(item, 100) });

            // 上流へ1マス足しても、同じ合流マス・同じ入力方向(左)の内部segmentが再び作られ、距離100のまま引き継ぐ
            // Adding an upstream cell recreates the internal segment for the same merge cell and input direction (left), keeping distance 100
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var next = Rebuild(old, world);
            AssertRun(next.Segments[InternalSegmentIndex(next)], (item, 100));
            Assert.AreEqual(1, ItemCount(next));
        }

        [Test]
        public void InternalSegmentItemIsDeletedWhenMergeDissolves()
        {
            var world = MachineMergeWorld();
            var old = Assemble(world);
            old.Segments[InternalSegmentIndex(old)].RestoreItems(new[] { new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromLeft), 100) });

            // 直進側を撤去すると合流でなくなり内部segmentも消える。走行中アイテムとしての救済もしない
            // Removing the straight input ends the merge and its internal segment; the item is not rescued as a running item
            world.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            Assert.IsFalse(next.Layouts.Any(layout => layout.IsInternal));
            Assert.AreEqual(0, ItemCount(next));
        }

        private static int ItemCount(BeltTransportAssembly assembly)
        {
            var count = 0;
            foreach (var segment in assembly.Segments)
            {
                count += segment.CaptureItems().Length;
                if (segment is BeltBufferedSegment buffered && buffered.Buffer.HasItem) count++;
            }
            return count;
        }

        // A(z=-1..0)とB(x=1から西向き)が合流M(z=1)に入り、C(z=2)へ流れる
        // A (z=-1..0) and B (westward from x=1) enter merge M (z=1), which flows to C (z=2)
        private static IWorldBlockDatastore MergeWorld()
        {
            var world = NewWorld();
            for (var z = -1; z <= 2; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            return world;
        }

        // z=0..2 の直線の z=1 へ、左(-X)の機械が内部segment経由で合流する
        // A machine on the left (-X) joins z=1 of the z=0..2 line through an internal segment
        private static IWorldBlockDatastore MachineMergeWorld()
        {
            var world = NewWorld();
            InstallMachinePorts(new[] { Vector3Int.right }, new[] { Vector3Int.back });
            for (var z = 0; z <= 2; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
            return world;
        }
    }
}
