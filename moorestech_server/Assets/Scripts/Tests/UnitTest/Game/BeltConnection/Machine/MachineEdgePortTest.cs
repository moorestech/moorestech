using System;
using System.Linq;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Interface;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Machine
{
    public class MachineEdgePortTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void UpperMachineOnlyOccludesItsOwnPortFace(bool facesEdge)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var lower = world.Place("LL", 2);
            var target = world.Place("UR", 1);
            // 入力面でも候補を選んでから方向を判定し、下側へ戻らない
            // Select even an input face before judging flow, without falling back to the lower belt
            var input = MachinePortTestTemplate.Input(Vector3Int.zero,
                new[] { facesEdge ? Vector3Int.forward : Vector3Int.right }, null);
            MachinePortTestTemplate.Install(new InventoryConnects(new[] { input }, Array.Empty<OutputConnectsElement>()), Vector3Int.one);
            MachinePortTestTemplate.Place(world, world.Position("UL"), BlockDirection.North);
            if (facesEdge) Assert.IsEmpty(BeltEdgeTestWorld.Connector(lower).ConnectedTargets);
            else Assert.AreSame(target, BeltEdgeTestWorld.Connector(lower).ConnectedTargets.Single().Value.TargetBlock);
            world.World.RemoveBlock(world.Position("UL"), BlockRemoveReason.ManualRemove);
            Assert.AreSame(target, BeltEdgeTestWorld.Connector(lower).ConnectedTargets.Single().Value.TargetBlock);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MachinePortShapesAndRealPortIdentityArePreserved(bool compatible)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var sourceShape = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var otherShape = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var output = MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.forward }, sourceShape);
            var input = MachinePortTestTemplate.Input(Vector3Int.zero, new[] { Vector3Int.back }, compatible ? sourceShape : otherShape);
            MachinePortTestTemplate.Install(new InventoryConnects(Array.Empty<InputConnectsElement>(), new[] { output }), Vector3Int.one);
            BeltPortTestTemplate.Install(new BeltPortTestTemplate(new InventoryConnects(new[] { input }, Array.Empty<OutputConnectsElement>())));
            var machine = MachinePortTestTemplate.Place(world, world.Position("UL"), BlockDirection.North);
            world.Place("UR", 1);
            var connections = BeltEdgeTestWorld.Connector(machine).ConnectedTargets;
            Assert.AreEqual(compatible ? 1 : 0, connections.Count);
            if (!compatible) return;
            Assert.AreSame(output, connections.Single().Value.SelfConnector);
            Assert.AreSame(input, connections.Single().Value.TargetConnector);
        }

        [Test]
        public void UnrestrictedInputProducesOnlyExternalHorizontalFaces()
        {
            var input = MachinePortTestTemplate.Input(new Vector3Int(1, 0, 1), null, null);
            var ports = new InventoryConnects(new[] { input }, null);
            var position = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(2, 1, 3));
            var edges = MachineInventoryEdgePorts.GetEdges(ports, position);
            Assert.AreEqual(1, edges.Count);
            Assert.AreEqual(new BeltEdge(new Vector3Int(1, 0, 1), Vector3Int.right, 0), edges.Single());
        }

        [Test]
        public void InternalOutsideAndNonHorizontalPortsDoNotCreateEdges()
        {
            var ports = new InventoryConnects(null, new[]
            {
                MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.forward }, null),
                MachinePortTestTemplate.Output(new Vector3Int(3, 0, 0), new[] { Vector3Int.right }, null),
                MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.up, Vector3Int.forward + Vector3Int.up }, null),
                MachinePortTestTemplate.Output(Vector3Int.zero, null, null)
            });
            var position = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsEmpty(MachineInventoryEdgePorts.GetEdges(ports, position));
        }

        [Test]
        public void DuplicateInputOutputFacesAreRecomputedOnce()
        {
            var input = MachinePortTestTemplate.Input(Vector3Int.zero, new[] { Vector3Int.back }, null);
            var output = MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.back, Vector3Int.back }, null);
            var ports = new InventoryConnects(new[] { input }, new[] { output });
            Assert.AreEqual(1, MachineInventoryEdgePorts.GetEdges(ports,
                new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one)).Count);
        }

        [Test]
        public void LaterMachineOutputCanWinTheShapeCheck()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var shape = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var accepted = MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.forward }, shape);
            MachinePortTestTemplate.Install(new InventoryConnects(null, new[]
            {
                MachinePortTestTemplate.Output(Vector3Int.zero, new[] { Vector3Int.forward }, other), accepted
            }), Vector3Int.one);
            BeltPortTestTemplate.Install(new BeltPortTestTemplate(new InventoryConnects(new[]
            {
                MachinePortTestTemplate.Input(Vector3Int.zero, new[] { Vector3Int.back }, shape)
            }, null)));
            var machine = MachinePortTestTemplate.Place(world, world.Position("UL"), BlockDirection.North);
            world.Place("UR", 1);
            Assert.AreSame(accepted, BeltEdgeTestWorld.Connector(machine).ConnectedTargets.Single().Value.SelfConnector);
        }
    }
}
