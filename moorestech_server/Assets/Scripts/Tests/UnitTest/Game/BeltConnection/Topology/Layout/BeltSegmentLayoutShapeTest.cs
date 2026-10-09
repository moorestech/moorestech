using Core.BeltTransport;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.Layout.BeltSegmentLayoutTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Topology.Layout
{
    public class BeltSegmentLayoutShapeTest
    {
        [Test]
        public void StraightLineBecomesOneNormalSegment()
        {
            var world = NewWorld();
            for (var z = 0; z < 3; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            var layouts = BuildLayouts(world);

            Assert.AreEqual(1, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, 2));
            Assert.IsEmpty(layouts[0].Inputs);
            Assert.IsEmpty(layouts[0].Outputs);
        }

        [Test]
        public void TMergeIsOneCellMergeWithDirectNormalInputs()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(-1, 0, 1), BlockDirection.East);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var layouts = BuildLayouts(world);

            // 座標順: 左(-1,0,1) → 合流(0,0,1) → 前(0,0,2) → 右(1,0,1)。内部segmentは作らない
            // Position order: left, merge, front, right; no internal segment is created
            Assert.AreEqual(4, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Right, new Vector3Int(-1, 0, 1));
            AssertSegment(layouts[1], BeltSegmentKind.Merge, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            AssertSegment(layouts[2], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, 2));
            AssertSegment(layouts[3], BeltSegmentKind.Normal, 6, BeltDirection.Left, new Vector3Int(1, 0, 1));

            // 合流の入力は方向順に通常segmentへ直結し、出力は前のsegmentへ
            // Merge inputs link directly to normal segments in direction order; its output goes to the front segment
            var merge = layouts[1];
            Assert.AreEqual(2, merge.Inputs.Length);
            AssertLink(merge.Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, 0);
            AssertLink(merge.Inputs[1], BeltDirection.Right, BeltEntryDirection.FromRight, 3);
            Assert.AreEqual(1, merge.Outputs.Length);
            AssertLink(merge.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 2);
            AssertLink(layouts[2].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 1);
            AssertLink(layouts[0].Outputs[0], BeltDirection.Right, BeltEntryDirection.FromLeft, 1);
            AssertLink(layouts[3].Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, 1);
            Assert.IsEmpty(layouts[0].Inputs);
            Assert.IsEmpty(layouts[3].Inputs);
        }

        [Test]
        public void SplitterIsBranchAndSpeedChangeCutsFeedingRun()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(-1, 0, 0), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 0), BlockDirection.East);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, -2), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var layouts = BuildLayouts(world);

            // 後ろの直線(速度6)は分配器(速度128)と速度が違うので分岐へ畳まれない
            // The rear run (speed 6) differs in speed from the splitter (128), so it is not folded into the branch
            Assert.AreEqual(5, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Left, new Vector3Int(-1, 0, 0));
            AssertSegment(layouts[1], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, -2), new Vector3Int(0, 0, -1));
            AssertSegment(layouts[2], BeltSegmentKind.Branch, 128, BeltDirection.Front, Vector3Int.zero);
            AssertSegment(layouts[3], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            AssertSegment(layouts[4], BeltSegmentKind.Normal, 6, BeltDirection.Right, new Vector3Int(1, 0, 0));

            AssertLink(layouts[1].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 2);
            var branch = layouts[2];
            Assert.AreEqual(1, branch.Inputs.Length);
            AssertLink(branch.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 1);
            // 分岐の出力は方向順(Front→Left→Right)で、各出力先は分岐を入力に持つ
            // Branch outputs follow direction order (Front, Left, Right) and each target lists the branch as its input
            Assert.AreEqual(3, branch.Outputs.Length);
            AssertLink(branch.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 3);
            AssertLink(branch.Outputs[1], BeltDirection.Left, BeltEntryDirection.FromRight, 0);
            AssertLink(branch.Outputs[2], BeltDirection.Right, BeltEntryDirection.FromLeft, 4);
            AssertLink(layouts[0].Inputs[0], BeltDirection.Right, BeltEntryDirection.FromRight, 2);
            AssertLink(layouts[3].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 2);
            AssertLink(layouts[4].Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, 2);
        }

        [Test]
        public void SameSpeedRunIsFoldedIntoBranch()
        {
            var world = NewWorld();
            Place(world, BeltTestMaster.Fast, new Vector3Int(0, 0, -2), BlockDirection.North);
            Place(world, BeltTestMaster.Fast, new Vector3Int(0, 0, -1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            var layouts = BuildLayouts(world);

            // 速度128の直線2マスは分配器と同速なので、分配器を末尾とする1本の分岐segmentに畳まれる
            // The two speed-128 straight cells match the splitter's speed, so they fold into one branch segment ending at the splitter
            Assert.AreEqual(2, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Branch, 128, BeltDirection.Front, new Vector3Int(0, 0, -2), new Vector3Int(0, 0, -1), Vector3Int.zero);
            Assert.IsEmpty(layouts[0].Inputs);
            Assert.AreEqual(1, layouts[0].Outputs.Length);
            AssertLink(layouts[0].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 1);
            AssertSegment(layouts[1], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            AssertLink(layouts[1].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 0);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SplitterIsBranchRegardlessOfConnectedOutputs(bool connectFront)
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            if (connectFront) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            var layouts = BuildLayouts(world);

            Assert.AreEqual(connectFront ? 2 : 1, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Branch, 128, BeltDirection.Front, Vector3Int.zero);
            Assert.IsEmpty(layouts[0].Inputs);
            Assert.AreEqual(connectFront ? 1 : 0, layouts[0].Outputs.Length);
            if (!connectFront) return;
            AssertLink(layouts[0].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 1);
            AssertLink(layouts[1].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 0);
        }

        [Test]
        public void SpeedChangeSplitsStraightRun()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 2), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 3), BlockDirection.North);
            var layouts = BuildLayouts(world);

            // 速度6→32の境で切れ、2本が1本の接続でつながる
            // The run is cut at the 6-to-32 speed change and the two segments are linked once
            Assert.AreEqual(2, layouts.Length);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1));
            AssertSegment(layouts[1], BeltSegmentKind.Normal, 32, BeltDirection.Front, new Vector3Int(0, 0, 2), new Vector3Int(0, 0, 3));
            Assert.IsEmpty(layouts[0].Inputs);
            AssertLink(layouts[0].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 1);
            AssertLink(layouts[1].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 0);
            Assert.IsEmpty(layouts[1].Outputs);
        }
    }
}
