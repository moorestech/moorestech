using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
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
    // 再構築で経路が変わったアイテムの進入方向合わせ、輪の再構築、同順位の復元順を確かめる
    // Checks entry-direction alignment of items whose path changed, ring rebuilds, and the restore order among equals
    public class BeltTransportRebuildPathTest
    {
        private static readonly ItemId ItemA = new(1);
        private static readonly Vector3Int Target = new(0, 0, 1);

        [Test]
        public void MismatchedEntryPrefersStraightInput()
        {
            // 西向きのマス(0,0,1)へ南(-Z)から入っていたアイテム。南を撤去し、直進側(+X)と前(+Z)から入る合流にする
            // An item that entered the west-facing cell (0,0,1) from the south (-Z); the south belt goes and inputs arrive straight (+X) and from the front (+Z)
            var world = NewWorld();
            var target = Place(world, ForUnitTestModBlockId.BeltConveyorId, Target, BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            var old = Assemble(world);
            var item = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, Target).RestoreItems(new[] { new BeltItemState(item, 120) });

            world.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.South);
            var next = Rebuild(old, world);

            // 方向順の先頭は前(FromFront)だが、直進(FromRight)を優先する。マス内進行量は保つ
            // The first input by direction is the front (FromFront), but the straight one (FromRight) wins; in-cell progress is kept
            Assert.AreEqual(BeltSegmentKind.Merge, SegmentAt(next, Target).Kind);
            AssertRun(SegmentAt(next, Target), (item.WithEntryDirection(BeltEntryDirection.FromRight), 120));
            Assert.AreEqual(target.BlockInstanceId, next.Layouts[SegmentIndexAt(next, Target)].Cells[0].BlockInstanceId);
        }

        [Test]
        public void MismatchedEntryWithoutStraightTakesFirstInputByDirection()
        {
            // 直進(+X)から入っていたアイテム。直進側を撤去し、後(-Z)と前(+Z)から入る合流にする
            // An item that entered straight (+X); the straight belt goes and inputs arrive from the back (-Z) and front (+Z)
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Target, BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            var old = Assemble(world);
            var item = NewItem(ItemA, BeltEntryDirection.FromRight);
            SegmentAt(old, Target).RestoreItems(new[] { new BeltItemState(item, 120) });

            world.RemoveBlock(new Vector3Int(1, 0, 1), BlockRemoveReason.ManualRemove);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.South);
            var next = Rebuild(old, world);

            // 直進が無いので方向順(Front<Back)の先頭FromFrontへ合わせる
            // Without a straight input it takes the first by direction order (Front<Back): FromFront
            AssertRun(SegmentAt(next, Target), (item.WithEntryDirection(BeltEntryDirection.FromFront), 120));
        }

        [Test]
        public void ItemOnCellWithoutInputsKeepsEntryDirection()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Target, BlockDirection.North);
            var old = Assemble(world);
            var item = NewItem(ItemA, BeltEntryDirection.FromBack);
            SegmentAt(old, Target).RestoreItems(new[] { new BeltItemState(item, 30) });

            // 入力の無いマスになっても、アイテムは進入方向を変えずにマス内の位置を保つ
            // When the cell loses every input, the item keeps its entry direction and its in-cell position
            world.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            AssertRun(SegmentAt(next, Target), (item, 30));
        }

        [Test]
        public void UnchangedRingRebuildKeepsEveryItem()
        {
            var world = RingWorld();
            var old = Assemble(world);
            var (onC3, onC2, onC0) = SeedRing(old);

            // 構成が変わらなければ全アイテムが同じ位置・同じ進入方向で戻る
            // With no change every item comes back at the same position with the same entry direction
            var next = Rebuild(old, world);
            AssertRun(next.Segments[0], (onC3, 0), (onC2, 300), (onC0, 900));
        }

        [Test]
        public void BreakingRingKeepsItemsOnRemainingCells()
        {
            var world = RingWorld();
            var old = Assemble(world);
            var (_, onC2, onC0) = SeedRing(old);

            // (1,0,0)を撤去すると (0,0,0)→(0,0,1)→(1,0,1) の直線になる。(1,0,0)上は消え、残りはマス内進行量を保つ
            // Removing (1,0,0) leaves the line (0,0,0)->(0,0,1)->(1,0,1); its item vanishes and the rest keep their in-cell progress
            world.RemoveBlock(new Vector3Int(1, 0, 0), BlockRemoveReason.ManualRemove);
            var next = Rebuild(old, world);
            Assert.AreEqual(1, next.Segments.Length);
            AssertRun(next.Segments[0], (onC2, 44), (onC0, 644));
        }

        [Test]
        public void RestoreCandidatesSortByPriorityDistanceSourceSegmentAndOrder()
        {
            var item = NewItem(ItemA, BeltEntryDirection.FromBack);
            var candidates = new List<BeltRestoreCandidate>
            {
                new(2, 0, 0, item, 0, 0),
                new(1, 0, 300, item, 0, 0),
                new(1, 0, 100, item, 5, 1),
                new(1, 0, 100, item, 5, 0),
                new(1, 0, 100, item, 2, 9),
            };

            // 優先度→出口までの距離→元のsegment番号→元の順番で並ぶ
            // Sorted by priority, then distance to exit, then source segment index, then source order
            candidates.Sort();
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 2 }, candidates.Select(c => c.Priority).ToArray());
            CollectionAssert.AreEqual(new[] { 100, 100, 100, 300, 0 }, candidates.Select(c => c.DistanceToExit).ToArray());
            Assert.AreEqual(new BeltRestoreCandidate(1, 0, 100, item, 2, 9).CompareTo(candidates[0]), 0);
            Assert.AreEqual(new BeltRestoreCandidate(1, 0, 100, item, 5, 0).CompareTo(candidates[1]), 0);
            Assert.AreEqual(new BeltRestoreCandidate(1, 0, 100, item, 5, 1).CompareTo(candidates[2]), 0);
        }

        // (0,0,0)北→(0,0,1)東→(1,0,1)南→(1,0,0)西→(0,0,0) の4マスの輪
        // A four-cell ring (0,0,0)N -> (0,0,1)E -> (1,0,1)S -> (1,0,0)W -> (0,0,0)
        private static IWorldBlockDatastore RingWorld()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.East);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.South);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 0), BlockDirection.West);
            return world;
        }

        // 輪は座標最小の(0,0,0)から始まる。末尾(1,0,0)・(1,0,1)・先頭(0,0,0)に1個ずつ置く
        // The ring starts at the smallest cell (0,0,0); put one item on the last cell (1,0,0), on (1,0,1) and on the head (0,0,0)
        private static (BeltItem, BeltItem, BeltItem) SeedRing(BeltTransportAssembly assembly)
        {
            Assert.AreEqual(1, assembly.Segments.Length);
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(1, 0, 0) },
                assembly.Layouts[0].Cells.Select(cell => cell.Position).ToArray());
            var onC3 = NewItem(ItemA, BeltEntryDirection.FromFront);
            var onC2 = NewItem(ItemA, BeltEntryDirection.FromLeft);
            var onC0 = NewItem(ItemA, BeltEntryDirection.FromRight);
            assembly.Segments[0].RestoreItems(new[] { new BeltItemState(onC3, 0), new BeltItemState(onC2, 300), new BeltItemState(onC0, 900) });
            return (onC3, onC2, onC0);
        }
    }
}
