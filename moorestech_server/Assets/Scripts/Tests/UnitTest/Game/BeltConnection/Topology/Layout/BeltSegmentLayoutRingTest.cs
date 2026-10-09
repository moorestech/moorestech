using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.Layout.BeltSegmentLayoutTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Topology.Layout
{
    // 輪の切れ目: 同速の輪は座標最小のマスを先頭に1本、速度が変わる輪は境界ごとに切って循環接続、合流を含む輪は合流で切れる
    // Ring cuts: a same-speed ring is one segment headed by its smallest cell, a speed-changing ring is cut at each boundary and linked in a cycle, a ring with a merge is cut at the merge
    public class BeltSegmentLayoutRingTest
    {
        [Test]
        public void SpeedChangingRingBecomesCircularlyLinkedSegments()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.East);
            Place(world, ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(1, 0, 1), BlockDirection.South);
            Place(world, ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(1, 0, 0), BlockDirection.West);
            var layouts = BuildLayouts(world);

            // 速度6→32と32→6の2か所で切れ、それぞれの先頭は境界の次のマス
            // Cut at the 6-to-32 and 32-to-6 boundaries; each head is the cell right after a boundary
            Assert.AreEqual(2, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Right, new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1));
            AssertSegment(layouts[1], BeltSegmentKind.Normal, 32, BeltDirection.Left, new Vector3Int(1, 0, 1), new Vector3Int(1, 0, 0));
            AssertLink(layouts[0].Outputs[0], BeltDirection.Right, BeltEntryDirection.FromLeft, 1);
            AssertLink(layouts[1].Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, 0);
            AssertLink(layouts[1].Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, 0);
            AssertLink(layouts[0].Inputs[0], BeltDirection.Right, BeltEntryDirection.FromRight, 1);
        }

        [Test]
        public void RingWithSideInputIsCutAtTheMerge()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.East);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.South);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 0), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(-1, 0, 0), BlockDirection.East);
            var layouts = BuildLayouts(world);

            // (0,0,0)は輪の後ろと外からの2入力で合流になり、残り3マスは合流の出口から始まる1本の通常
            // (0,0,0) gets two inputs (ring rear and outside) and becomes a merge; the other three cells form one normal starting at the merge exit
            Assert.AreEqual(3, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Right, new Vector3Int(-1, 0, 0));
            AssertSegment(layouts[1], BeltSegmentKind.Merge, 6, BeltDirection.Front, new Vector3Int(0, 0, 0));
            AssertSegment(layouts[2], BeltSegmentKind.Normal, 6, BeltDirection.Left, new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(1, 0, 0));

            // 合流の入力は方向順(Left=外, Right=輪)で、どちらも通常segmentなので内部segmentは無い
            // Merge inputs follow direction order (Left = outside, Right = ring); both are normal segments, so no internal segment exists
            Assert.AreEqual(2, layouts[1].Inputs.Length);
            AssertLink(layouts[1].Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, 0);
            AssertLink(layouts[1].Inputs[1], BeltDirection.Right, BeltEntryDirection.FromRight, 2);
            AssertLink(layouts[1].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 2);
            AssertLink(layouts[2].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 1);
            AssertLink(layouts[2].Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, 1);
        }

        [Test]
        public void RingBecomesOneNormalSegmentLinkedToItself()
        {
            var layouts = BuildRing(new[] { 0, 1, 2, 3 });

            // 輪は座標最小のマスを先頭に搬送順で並び、入出力とも自分自身を指す
            // A ring starts at its smallest-position cell in transport order and links to itself on both ends
            Assert.AreEqual(1, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Left,
                new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(1, 0, 0));
            Assert.AreEqual(1, layouts[0].Inputs.Length);
            Assert.AreEqual(1, layouts[0].Outputs.Length);
            AssertLink(layouts[0].Inputs[0], BeltDirection.Right, BeltEntryDirection.FromRight, 0);
            AssertLink(layouts[0].Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, 0);
        }

        [Test]
        public void RingHeadIsSmallestCellRegardlessOfPlacementOrder()
        {
            var forward = Signature(BuildRing(new[] { 0, 1, 2, 3 }));
            var reverse = Signature(BuildRing(new[] { 3, 2, 1, 0 }));
            CollectionAssert.AreEqual(forward, reverse);
            Assert.AreEqual(new Vector3Int(0, 0, 0), BuildRing(new[] { 2, 3, 0, 1 })[0].Cells[0].Position);
        }

        private static BeltSegmentLayout[] BuildRing(int[] order)
        {
            var positions = new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(1, 0, 0) };
            var directions = new[] { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };
            var world = NewWorld();
            foreach (var index in order) Place(world, ForUnitTestModBlockId.BeltConveyorId, positions[index], directions[index]);
            return BuildLayouts(world);
        }
    }
}
