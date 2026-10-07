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

        [TestCase(BlockDirection.North, false, false)]
        [TestCase(BlockDirection.East, false, false)]
        [TestCase(BlockDirection.South, false, false)]
        [TestCase(BlockDirection.West, false, false)]
        [TestCase(BlockDirection.North, true, false)]
        [TestCase(BlockDirection.East, true, false)]
        [TestCase(BlockDirection.South, true, false)]
        [TestCase(BlockDirection.West, true, false)]
        [TestCase(BlockDirection.North, false, true)]
        [TestCase(BlockDirection.East, false, true)]
        [TestCase(BlockDirection.South, false, true)]
        [TestCase(BlockDirection.West, false, true)]
        [TestCase(BlockDirection.North, true, true)]
        [TestCase(BlockDirection.East, true, true)]
        [TestCase(BlockDirection.South, true, true)]
        [TestCase(BlockDirection.West, true, true)]
        public void NonOriginPortUsesItsRotatedFace(BlockDirection rotation, bool machineSource, bool machineFirst)
        {
            var world = new BeltEdgeTestWorld(false, rotation);
            var offset = new Vector3Int(1, 1, 2);
            var input = MachinePortTestTemplate.Input(offset, new[] { Vector3Int.forward }, null);
            var output = MachinePortTestTemplate.Output(offset, new[] { Vector3Int.forward }, null);
            MachinePortTestTemplate.Install(new InventoryConnects(new[] { input }, new[] { output }), new Vector3Int(2, 2, 3));
            var position = new BlockPositionInfo(new Vector3Int(4, 2, 5), rotation, new Vector3Int(2, 2, 3));
            if (machineFirst) MachinePortTestTemplate.Place(world, position.OriginalPos, rotation);
            var cell = position.ConvertBlockLocalToWorldCell(offset);
            var outward = rotation.ConvertLocalCell(Vector3Int.forward);
            var beltDirection = machineSource ? rotation : rotation.HorizonRotation().HorizonRotation();
            Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, cell + outward, beltDirection,
                Array.Empty<BlockCreateParam>(), out var belt));
            if (!machineFirst) MachinePortTestTemplate.Place(world, position.OriginalPos, rotation);
            var machine = world.World.GetBlock(position.OriginalPos);
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
        public void RemovingOneBeltPreservesOtherEdgesAndMachineConnections()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var machine = MachinePortTestTemplate.Place(world, Vector3Int.zero, BlockDirection.North);
            var neighbor = MachinePortTestTemplate.Place(world, Vector3Int.right, BlockDirection.North);
            Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.forward, BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var front));
            Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.back, BlockDirection.South,
                Array.Empty<BlockCreateParam>(), out var back));
            var connector = BeltEdgeTestWorld.Connector(machine);
            CollectionAssert.AreEquivalent(new[] { neighbor, front, back }, connector.ConnectedTargets.Values.Select(info => info.TargetBlock));

            // 一面の撤去で別面の接続を消さない
            // Removing one face's neighbor must preserve connections on the other faces
            Assert.IsTrue(world.World.RemoveBlock(Vector3Int.forward, BlockRemoveReason.ManualRemove));
            CollectionAssert.AreEquivalent(new[] { neighbor, back }, connector.ConnectedTargets.Values.Select(info => info.TargetBlock));
            Assert.AreSame(machine, BeltEdgeTestWorld.Connector(neighbor).ConnectedTargets.Single().Value.TargetBlock);
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
