using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using Tests.UnitTest.Game.BeltConnection.Machine;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Topology
{
    public class BeltTopologyMachineTest
    {
        private const BeltTopologyPartnerKind Machine = BeltTopologyPartnerKind.Machine;
        private const BeltTopologyPartnerKind Belt = BeltTopologyPartnerKind.Belt;

        [Test]
        public void MultiCellMachineUsesConnectorCell()
        {
            var world = NewWorld();
            var input = MachinePortTestTemplate.Input(new Vector3Int(0, 0, 0), new[] { Vector3Int.back }, null);
            var output = MachinePortTestTemplate.Output(new Vector3Int(1, 0, 1), new[] { Vector3Int.forward }, null);
            MachinePortTestTemplate.Install(new InventoryConnects(new[] { input }, new[] { output }), new Vector3Int(2, 1, 2));
            var machine = Place(world, ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North);
            var feeder = Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var receiver = Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 2), BlockDirection.North);
            var cells = BeltTopologyBuilder.Build(world);

            // 2x2機械の入口は原点セル(0,0,0)、出口はoffset(1,0,1)のセルとして報告される
            // On the 2x2 machine the inlet is reported at origin cell (0,0,0) and the outlet at offset cell (1,0,1)
            var toMachine = Cell(cells, new Vector3Int(0, 0, -1)).Outputs[0];
            AssertConnection(toMachine, BeltDirection.Front, BeltEntryDirection.FromBack, Machine, new Vector3Int(0, 0, 0));
            Assert.AreSame(machine, toMachine.PartnerBlock);
            Assert.AreSame(input, toMachine.TargetConnector);
            Assert.AreSame(machine.ComponentManager.GetComponent<IBlockInventory>(), toMachine.ReceiverInventory);
            Assert.AreSame(BeltOutputConnector(feeder), toMachine.SourceConnector);

            var fromMachine = Cell(cells, new Vector3Int(1, 0, 2)).Inputs[0];
            AssertConnection(fromMachine, BeltDirection.Back, BeltEntryDirection.FromBack, Machine, new Vector3Int(1, 0, 1));
            Assert.AreSame(machine, fromMachine.PartnerBlock);
            Assert.AreSame(output, fromMachine.SourceConnector);
            Assert.AreSame(receiver.ComponentManager.GetComponent<IBlockInventory>(), fromMachine.ReceiverInventory);
            Assert.AreEqual(2, cells.Count);
        }

        [Test]
        public void UpSlopeFeedsMachineOneCellHigher()
        {
            var world = NewWorld();
            var input = MachinePortTestTemplate.Input(Vector3Int.zero, null, null);
            MachinePortTestTemplate.Install(new InventoryConnects(new[] { input }, null), Vector3Int.one);
            Place(world, BeltTestMaster.Up, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 1, 1), BlockDirection.North);

            // 上り坂(y0)の上端edgeは高さ1で機械セル(0,1,1)に接し、送り側が低いのでBelow
            // The Up slope (y0) top edge at height 1 touches machine cell (0,1,1); the sender is lower, so the entry is Below
            var slope = Cell(BeltTopologyBuilder.Build(world), Vector3Int.zero);
            AssertConnection(slope.Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBackBelow, Machine, new Vector3Int(0, 1, 1));
        }

        [Test]
        public void TwoMachinesAndBeltFeedSameCell()
        {
            var cells = BuildTwoMachineWorld(new[] { 0, 1, 2, 3 }, out var leftPort, out var rightPort);
            var merged = Cell(cells, new Vector3Int(0, 0, 1));

            Assert.AreEqual(3, merged.Inputs.Length);
            AssertConnection(merged.Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, Belt, new Vector3Int(0, 0, 0));
            AssertConnection(merged.Inputs[1], BeltDirection.Left, BeltEntryDirection.FromLeft, Machine, new Vector3Int(-1, 0, 1));
            AssertConnection(merged.Inputs[2], BeltDirection.Right, BeltEntryDirection.FromRight, Machine, new Vector3Int(1, 0, 1));
            // 左の機械は右向きポート、右の機械は左向きポートから出す
            // The left machine emits from its right-facing port and the right machine from its left-facing port
            Assert.AreSame(rightPort, merged.Inputs[1].SourceConnector);
            Assert.AreSame(leftPort, merged.Inputs[2].SourceConnector);
        }

        [Test]
        public void MachinePlacementOrderDoesNotChangeResult()
        {
            var forward = Signature(BuildTwoMachineWorld(new[] { 0, 1, 2, 3 }, out _, out _));
            var reverse = Signature(BuildTwoMachineWorld(new[] { 3, 2, 1, 0 }, out _, out _));
            CollectionAssert.AreEqual(forward, reverse);
        }

        private static List<BeltTopologyCell> BuildTwoMachineWorld(int[] order, out OutputConnectsElement leftPort, out OutputConnectsElement rightPort)
        {
            var world = NewWorld();
            leftPort = MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.left }, null);
            rightPort = MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.right }, null);
            MachinePortTestTemplate.Install(new InventoryConnects(null, new[] { leftPort, rightPort }), Vector3Int.one);

            // 0:後ろのベルト 1:合流先ベルト 2:左の機械 3:右の機械
            // 0: rear belt, 1: merge belt, 2: left machine, 3: right machine
            foreach (var index in order)
            {
                if (index == 0) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
                if (index == 1) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
                if (index == 2) Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
                if (index == 3) Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 1), BlockDirection.North);
            }
            return BeltTopologyBuilder.Build(world);
        }

        private static IBlockConnector BeltOutputConnector(IBlock belt)
        {
            // 接続に使われるのはマスタに定義された出力コネクターそのもの
            // The connection uses the very output connector object defined in the master
            return ((BeltConveyorBlockParam)belt.BlockMasterElement.BlockParam).InventoryConnectors.OutputConnects[0];
        }
    }
}
