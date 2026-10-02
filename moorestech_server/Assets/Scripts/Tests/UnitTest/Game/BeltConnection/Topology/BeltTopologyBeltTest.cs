using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Topology
{
    public class BeltTopologyBeltTest
    {
        private const BeltTopologyPartnerKind Belt = BeltTopologyPartnerKind.Belt;

        [Test]
        public void StraightLineHasLevelBackEntries()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            for (var z = 0; z < 3; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            var cells = BeltTopologyBuilder.Build(world);

            Assert.AreEqual(3, cells.Count);
            var first = cells[0];
            Assert.AreEqual(new Vector3Int(0, 0, 0), first.Position);
            Assert.AreEqual(BeltDirection.Front, first.Forward);
            Assert.AreEqual(6, first.BeltSpeedPerTick);
            Assert.IsFalse(first.IsSplitter);
            Assert.IsEmpty(first.Inputs);
            AssertConnection(first.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 1));
            var middle = cells[1];
            AssertConnection(middle.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 0));
            AssertConnection(middle.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 2));
            Assert.IsEmpty(cells[2].Outputs);
            Assert.AreEqual(1, cells[2].Inputs.Length);
        }

        [Test]
        public void TurnEntersStraightBeltFromSide()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            var cells = BeltTopologyBuilder.Build(world);

            // 右(+X)から西向きベルトが入り、入力は方向値の順(Back→Right)に並ぶ
            // A west-facing belt enters from the right (+X); inputs are ordered by direction value (Back, Right)
            var straight = Cell(cells, new Vector3Int(0, 0, 1));
            Assert.AreEqual(2, straight.Inputs.Length);
            AssertConnection(straight.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 0));
            AssertConnection(straight.Inputs[1], BeltDirection.Right, BeltEntryDirection.FromRight, Belt, new Vector3Int(1, 0, 1));
            var side = Cell(cells, new Vector3Int(1, 0, 1));
            Assert.AreEqual(BeltDirection.Left, side.Forward);
            AssertConnection(side.Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, Belt, new Vector3Int(0, 0, 1));
        }

        [Test]
        public void TMergeReportsBothSideInputs()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(-1, 0, 1), BlockDirection.East);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var center = Cell(BeltTopologyBuilder.Build(world), new Vector3Int(0, 0, 1));

            Assert.AreEqual(2, center.Inputs.Length);
            AssertConnection(center.Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, Belt, new Vector3Int(-1, 0, 1));
            AssertConnection(center.Inputs[1], BeltDirection.Right, BeltEntryDirection.FromRight, Belt, new Vector3Int(1, 0, 1));
            AssertConnection(center.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 2));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SplitterFlagFollowsMasterOutputsNotConnections(bool connectAllOutputs)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            if (connectAllOutputs)
            {
                Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(-1, 0, 0), BlockDirection.West);
                Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 0), BlockDirection.East);
            }
            var splitter = Cell(BeltTopologyBuilder.Build(world), Vector3Int.zero);

            Assert.IsTrue(splitter.IsSplitter);
            Assert.AreEqual(128, splitter.BeltSpeedPerTick);
            Assert.AreEqual(connectAllOutputs ? 3 : 1, splitter.Outputs.Length);
            AssertConnection(splitter.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 1));
            if (!connectAllOutputs) return;
            AssertConnection(splitter.Outputs[1], BeltDirection.Left, BeltEntryDirection.FromRight, Belt, new Vector3Int(-1, 0, 0));
            AssertConnection(splitter.Outputs[2], BeltDirection.Right, BeltEntryDirection.FromLeft, Belt, new Vector3Int(1, 0, 0));
        }

        [Test]
        public void UpSlopeRaisesEntryFromBelow()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, BeltTestMaster.Up, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 1, 2), BlockDirection.North);
            var cells = BeltTopologyBuilder.Build(world);

            // 平地(y0)→上り坂(y0)は同じ高さで水平、上り坂(y0)→平地(y1)は送り側が低いのでBelow
            // Flat(y0)->Up(y0) is level; Up(y0)->flat(y1) has the lower sender, so the entry is Below
            var slope = Cell(cells, new Vector3Int(0, 0, 1));
            AssertConnection(slope.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 0));
            AssertConnection(slope.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBackBelow, Belt, new Vector3Int(0, 1, 2));
            AssertConnection(Cell(cells, new Vector3Int(0, 1, 2)).Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBackBelow, Belt, new Vector3Int(0, 0, 1));
        }

        [Test]
        public void DownSlopeLowersEntryFromAbove()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 1, 0), BlockDirection.North);
            Place(world, BeltTestMaster.Down, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var cells = BeltTopologyBuilder.Build(world);

            // 平地(y1)→下り坂(y0)は送り側が高いのでAbove、下り坂(y0)→平地(y0)は水平
            // Flat(y1)->Down(y0) has the higher sender, so the entry is Above; Down(y0)->flat(y0) is level
            AssertConnection(Cell(cells, new Vector3Int(0, 1, 0)).Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBackAbove, Belt, new Vector3Int(0, 0, 1));
            var slope = Cell(cells, new Vector3Int(0, 0, 1));
            AssertConnection(slope.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBackAbove, Belt, new Vector3Int(0, 1, 0));
            AssertConnection(slope.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 2));
        }

        [Test]
        public void NonHorizontalBeltIsExcludedWithoutWarning()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.UpNorth);
            Assert.IsEmpty(BeltTopologyBuilder.Build(world));
        }

        [Test]
        public void PlacementOrderDoesNotChangeResult()
        {
            var positions = new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(-1, 0, 1), new Vector3Int(0, 0, 2) };
            var directions = new[] { BlockDirection.North, BlockDirection.North, BlockDirection.West, BlockDirection.East, BlockDirection.East };
            var forward = BuildInOrder(new[] { 0, 1, 2, 3, 4 });
            var reverse = BuildInOrder(new[] { 4, 3, 2, 1, 0 });
            CollectionAssert.AreEqual(forward, reverse);

            List<string> BuildInOrder(int[] order)
            {
                var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
                foreach (var index in order) Place(world, ForUnitTestModBlockId.BeltConveyorId, positions[index], directions[index]);
                return Signature(BeltTopologyBuilder.Build(world));
            }
        }
    }
}
