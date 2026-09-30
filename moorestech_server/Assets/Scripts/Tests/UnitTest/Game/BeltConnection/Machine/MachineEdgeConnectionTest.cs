using System;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Machine
{
    public class MachineEdgeConnectionTest
    {
        [Test]
        public void MultiBlockConnectTest()
        {
            var testWorld = new BeltEdgeTestWorld(false, BlockDirection.North);
            var world = testWorld.World;
            var outputs = new[]
            {
                MachinePortTestTemplate.Output(new Vector3Int(2, 0, 1), new[] { Vector3Int.forward }, null),
                MachinePortTestTemplate.Output(new Vector3Int(2, 0, 0), new[] { Vector3Int.back }, null)
            };
            MachinePortTestTemplate.Install(new InventoryConnects(null, outputs), new Vector3Int(3, 1, 2));
            // 複数の外面ポートには隣接するベルトだけを接続する
            // Connect only the adjacent belts to multiple exterior port faces
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(2, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var front);
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(2, 0, -1), BlockDirection.South, Array.Empty<BlockCreateParam>(), out var back);
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(3, 0, 3), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, -1), BlockDirection.South, Array.Empty<BlockCreateParam>(), out _);
            var machine = MachinePortTestTemplate.Place(testWorld, Vector3Int.zero, BlockDirection.North);
            CollectionAssert.AreEquivalent(new[] { front, back }, BeltEdgeTestWorld.Connector(machine).ConnectedTargets.Values.Select(info => info.TargetBlock));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void MachineFaceConnectsToLowerSlopeInBothPlacementOrders(bool machineSource, bool machineFirst)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var machineCell = world.Position(machineSource ? "UL" : "UR");
            if (machineFirst) MachinePortTestTemplate.Place(world, machineCell, BlockDirection.North);
            var belt = world.Place(machineSource ? "LR" : "LL", machineSource ? 3 : 2);
            if (!machineFirst) MachinePortTestTemplate.Place(world, machineCell, BlockDirection.North);
            var machine = world.World.GetBlock(machineCell);
            var source = machineSource ? machine : belt;
            var target = machineSource ? belt : machine;
            Assert.AreSame(target, BeltEdgeTestWorld.Connector(source).ConnectedTargets.Single().Value.TargetBlock);

            // 機械撤去では下側セルのベルト辞書も更新される
            // Removing a machine also updates a belt dictionary in the lower cell
            world.World.RemoveBlock(machineCell, BlockRemoveReason.ManualRemove);
            Assert.IsEmpty(BeltEdgeTestWorld.Connector(belt).ConnectedTargets);
        }

        [TestCase(BlockDirection.North, false)]
        [TestCase(BlockDirection.East, false)]
        [TestCase(BlockDirection.South, false)]
        [TestCase(BlockDirection.West, false)]
        [TestCase(BlockDirection.North, true)]
        [TestCase(BlockDirection.East, true)]
        [TestCase(BlockDirection.South, true)]
        [TestCase(BlockDirection.West, true)]
        public void NonOriginPortUsesItsRotatedFace(BlockDirection rotation, bool machineSource)
        {
            var world = new BeltEdgeTestWorld(false, rotation);
            var offset = new Vector3Int(1, 1, 2);
            var input = MachinePortTestTemplate.Input(offset, new[] { Vector3Int.forward }, null);
            var output = MachinePortTestTemplate.Output(offset, new[] { Vector3Int.forward }, null);
            MachinePortTestTemplate.Install(new InventoryConnects(new[] { input }, new[] { output }), new Vector3Int(2, 2, 3));
            var machine = MachinePortTestTemplate.Place(world, new Vector3Int(4, 2, 5), rotation);
            var cell = machine.BlockPositionInfo.ConvertBlockLocalToWorldCell(offset);
            var outward = rotation.ConvertLocalCell(Vector3Int.forward);
            var beltDirection = machineSource ? rotation : rotation.HorizonRotation().HorizonRotation();
            Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, cell + outward, beltDirection,
                Array.Empty<BlockCreateParam>(), out var belt));
            var source = machineSource ? machine : belt;
            var target = machineSource ? belt : machine;
            var connection = BeltEdgeTestWorld.Connector(source).ConnectedTargets.Single().Value;
            Assert.AreSame(target, connection.TargetBlock);
            Assert.AreSame(machineSource ? output : input, machineSource ? connection.SelfConnector : connection.TargetConnector);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingMachineRestoresThirdPartyConnection(bool machineSource)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var lower = world.Place("LL", 2);
            var down = world.Place("LR", 3);
            var existing = world.Place(machineSource ? "UR" : "UL", 1);
            var cell = world.Position(machineSource ? "UL" : "UR");
            var machine = MachinePortTestTemplate.Place(world, cell, BlockDirection.North);
            // 上側の機械が同じedgeの相手を差し替える
            // The upper machine replaces the partner on the same edge
            Assert.IsEmpty(BeltEdgeTestWorld.Connector(lower).ConnectedTargets);
            var source = machineSource ? machine : existing;
            Assert.AreSame(machineSource ? existing : machine, BeltEdgeTestWorld.Connector(source).ConnectedTargets.Single().Value.TargetBlock);
            world.World.RemoveBlock(cell, BlockRemoveReason.ManualRemove);
            Assert.AreSame(machineSource ? existing : down,
                BeltEdgeTestWorld.Connector(machineSource ? lower : existing).ConnectedTargets.Single().Value.TargetBlock);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MachineConnectionsRebuildInEitherLoadOrder(bool reverse)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            world.Place("LL", 2);
            MachinePortTestTemplate.Place(world, world.Position("UR"), BlockDirection.North);
            var save = world.World.GetSaveJsonObject();
            if (reverse) save.Reverse();
            var loaded = new BeltEdgeTestWorld(false, BlockDirection.North);
            loaded.World.LoadBlockDataList(save);
            var source = loaded.World.GetBlock(loaded.Position("LL"));
            Assert.AreSame(loaded.World.GetBlock(loaded.Position("UR")), BeltEdgeTestWorld.Connector(source).ConnectedTargets.Single().Value.TargetBlock);
        }

        [Test]
        public void MachineToMachineStillUsesTheExistingConnectionPath()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var first = MachinePortTestTemplate.Place(world, Vector3Int.zero, BlockDirection.North);
            var second = MachinePortTestTemplate.Place(world, Vector3Int.forward, BlockDirection.North);
            Assert.AreSame(second, BeltEdgeTestWorld.Connector(first).ConnectedTargets.Single().Value.TargetBlock);
            Assert.AreSame(first, BeltEdgeTestWorld.Connector(second).ConnectedTargets.Single().Value.TargetBlock);
            world.World.RemoveBlock(Vector3Int.forward, BlockRemoveReason.ManualRemove);
            Assert.IsEmpty(BeltEdgeTestWorld.Connector(first).ConnectedTargets);
        }
    }
}
