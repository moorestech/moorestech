using System.Linq;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using Tests.UnitTest.Game.BeltConnection.Machine;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Topology
{
    public class BeltTopologyMachineInputRuleTest
    {
        private const BeltTopologyPartnerKind Machine = BeltTopologyPartnerKind.Machine;
        private const BeltTopologyPartnerKind Belt = BeltTopologyPartnerKind.Belt;
        private static readonly Vector3Int Center = new(0, 0, 1);
        private static readonly Vector3Int Rear = new(0, 0, 0);
        private static readonly Vector3Int LeftSide = new(-1, 0, 1);
        private static readonly Vector3Int RightSide = new(1, 0, 1);

        [Test]
        public void BeltInputExcludesMachineInput()
        {
            var world = CreateWorldWithMachineTemplate();
            var rear = Place(world, ForUnitTestModBlockId.BeltConveyorId, Rear, BlockDirection.North);
            var center = Place(world, ForUnitTestModBlockId.BeltConveyorId, Center, BlockDirection.North);
            var machine = PlaceMachine(world, LeftSide);
            var cell = Cell(BeltTopologyBuilder.Build(world), Center);

            Assert.AreEqual(1, cell.Inputs.Length);
            AssertConnection(cell.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Belt, Rear);
            Assert.IsTrue(Accepts(cell, rear));
            Assert.IsFalse(Accepts(cell, machine));
            Assert.AreSame(center, cell.Block);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TwoMachinesKeepSmallerCoordinateRegardlessOfPlacementOrder(bool rightFirst)
        {
            var world = CreateWorldWithMachineTemplate();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Center, BlockDirection.North);
            IBlock left;
            IBlock right;
            if (rightFirst)
            {
                right = PlaceMachine(world, RightSide);
                left = PlaceMachine(world, LeftSide);
            }
            else
            {
                left = PlaceMachine(world, LeftSide);
                right = PlaceMachine(world, RightSide);
            }
            var cell = Cell(BeltTopologyBuilder.Build(world), Center);

            // X が小さい左の機械だけが残る
            // Only the left machine, which has the smaller X, remains
            Assert.AreEqual(1, cell.Inputs.Length);
            AssertConnection(cell.Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, Machine, LeftSide);
            Assert.AreSame(left, cell.Inputs[0].PartnerBlock);
            Assert.IsTrue(Accepts(cell, left));
            Assert.IsFalse(Accepts(cell, right));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThreeMachinesKeepOnlySmallestCoordinate(bool reverseOrder)
        {
            var world = CreateWorldWithMachineTemplate();
            var positions = reverseOrder ? new[] { Rear, RightSide, LeftSide } : new[] { LeftSide, RightSide, Rear };
            var machines = positions.Select(position => PlaceMachine(world, position)).ToArray();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Center, BlockDirection.North);
            var cell = Cell(BeltTopologyBuilder.Build(world), Center);

            Assert.AreEqual(1, cell.Inputs.Length);
            AssertConnection(cell.Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, Machine, LeftSide);
            foreach (var machine in machines)
                Assert.AreEqual(machine.BlockPositionInfo.OriginalPos == LeftSide, Accepts(cell, machine), $"machine at {machine.BlockPositionInfo.OriginalPos}");
        }

        [Test]
        public void SingleMachineInputIsKept()
        {
            var world = CreateWorldWithMachineTemplate();
            var machine = PlaceMachine(world, Rear);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Center, BlockDirection.North);
            var cell = Cell(BeltTopologyBuilder.Build(world), Center);

            Assert.AreEqual(1, cell.Inputs.Length);
            AssertConnection(cell.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Machine, Rear);
            Assert.IsTrue(Accepts(cell, machine));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MachineFeedingSplitterOrSlopeBackIsKept(bool slope)
        {
            var world = CreateWorldWithMachineTemplate();
            var machine = PlaceMachine(world, Rear);
            Place(world, slope ? BeltTestMaster.Up : ForUnitTestModBlockId.GearBeltConveyorSplitter, Center, BlockDirection.North);
            var cell = Cell(BeltTopologyBuilder.Build(world), Center);

            Assert.AreEqual(1, cell.Inputs.Length);
            AssertConnection(cell.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Machine, Rear);
            Assert.IsTrue(Accepts(cell, machine));
        }

        [Test]
        public void BeltToMachineOutputsAreNotRestricted()
        {
            var world = CreateWorldWithMachineTemplate();
            var front = new Vector3Int(0, 0, 2);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Rear, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Center, BlockDirection.North);
            PlaceMachine(world, front);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 2), BlockDirection.West);
            PlaceMachine(world, LeftSide);
            var cells = BeltTopologyBuilder.Build(world);

            // 中央は横の機械入力を外されても、前の機械への出力は残る。別のベルトも同じ機械へ出せる
            // The center loses its side machine input yet keeps its output to the front machine; another belt also feeds that machine
            var center = Cell(cells, Center);
            Assert.AreEqual(1, center.Inputs.Length);
            AssertConnection(center.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Belt, Rear);
            Assert.AreEqual(1, center.Outputs.Length);
            AssertConnection(center.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, Machine, front);
            var side = Cell(cells, new Vector3Int(1, 0, 2));
            Assert.AreEqual(1, side.Outputs.Length);
            AssertConnection(side.Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, Machine, front);
        }

        [Test]
        public void BeltMergeKeepsBothBeltsAndDropsMachine()
        {
            var world = CreateWorldWithMachineTemplate();
            var machine = PlaceMachine(world, Rear);
            var left = Place(world, ForUnitTestModBlockId.BeltConveyorId, LeftSide, BlockDirection.East);
            var right = Place(world, ForUnitTestModBlockId.BeltConveyorId, RightSide, BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Center, BlockDirection.North);
            var cell = Cell(BeltTopologyBuilder.Build(world), Center);

            Assert.AreEqual(2, cell.Inputs.Length);
            AssertConnection(cell.Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, Belt, LeftSide);
            AssertConnection(cell.Inputs[1], BeltDirection.Right, BeltEntryDirection.FromRight, Belt, RightSide);
            Assert.IsTrue(Accepts(cell, left));
            Assert.IsTrue(Accepts(cell, right));
            Assert.IsFalse(Accepts(cell, machine));
        }

        private static IWorldBlockDatastore CreateWorldWithMachineTemplate()
        {
            // 1マスの機械。四方へ出力でき、入力は全側面で受ける
            // A one-cell machine that can output to all four sides and accepts input on every side
            var world = new BeltEdgeTestWorld(false, BlockDirection.North).World;
            var outputs = new[] { Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back }
                .Select(direction => MachinePortTestTemplate.Output(Vector3Int.zero, new[] { direction }, null)).ToArray();
            MachinePortTestTemplate.Install(new InventoryConnects(new[] { MachinePortTestTemplate.Input(Vector3Int.zero, null, null) }, outputs), Vector3Int.one);
            return world;
        }

        private static IBlock PlaceMachine(IWorldBlockDatastore world, Vector3Int position)
        {
            return Place(world, ForUnitTestModBlockId.ChestId, position, BlockDirection.North);
        }

        // 送り側が実際に持つ接続情報で問い合わせる。後段で送り側がInsertItemするときと同じ組
        // Query with the sender's real connection info, the same tuple a later InsertItem from the sender carries
        private static bool Accepts(BeltTopologyCell cell, IBlock source)
        {
            var targets = source.ComponentManager.GetComponent<IBlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Values;
            var info = targets.Single(connected => ReferenceEquals(connected.TargetBlock, cell.Block));
            return cell.AcceptsInput(source.BlockInstanceId, info.SelfConnector, info.TargetConnector);
        }
    }
}
