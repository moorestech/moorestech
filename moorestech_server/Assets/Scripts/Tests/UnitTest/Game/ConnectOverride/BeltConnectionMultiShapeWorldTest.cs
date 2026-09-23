using System;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.ConnectOverride
{
    public class BeltConnectionMultiShapeWorldTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SelectedInputOwnerSearchesBothShapes(bool reverseOrder)
        {
            CreateWorld();
            var world = ServerContext.WorldBlockDatastore;
            var targetId = reverseOrder
                ? ForUnitTestModBlockId.TestBeltMultiInputBA
                : ForUnitTestModBlockId.TestBeltMultiInputAB;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeDown, Vector3Int.up,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source));
            Assert.IsTrue(world.TryAddBlock(targetId, Vector3Int.up + Vector3Int.forward,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var target));
            Assert.IsTrue(Connector(source).ConnectedTargets.ContainsKey(
                target.GetComponent<VanillaBeltConveyorComponent>()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelectedOutputOwnerSearchesBothShapes(bool reverseOrder)
        {
            CreateWorld();
            var world = ServerContext.WorldBlockDatastore;
            var sourceId = reverseOrder
                ? ForUnitTestModBlockId.TestBeltMultiOutputBA
                : ForUnitTestModBlockId.TestBeltMultiOutputAB;
            Assert.IsTrue(world.TryAddBlock(sourceId, Vector3Int.up,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeTarget,
                Vector3Int.up + Vector3Int.forward, BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var target));
            Assert.IsTrue(Connector(source).ConnectedTargets.ContainsKey(
                target.GetComponent<VanillaBeltConveyorComponent>()));
        }

        [Test]
        public void IncompatibleSelectedOwnerDoesNotFallBackToCompatibleLowerOwner()
        {
            CreateWorld();
            var world = ServerContext.WorldBlockDatastore;
            var upperPosition = Vector3Int.up;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeUp, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var lower));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeTarget,
                Vector3Int.up + Vector3Int.forward, BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var target));
            var targetInventory = target.GetComponent<VanillaBeltConveyorComponent>();
            Assert.IsTrue(Connector(lower).ConnectedTargets.ContainsKey(targetInventory));

            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltMultiOutputAA,
                upperPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var upper));
            Assert.IsFalse(Connector(lower).ConnectedTargets.ContainsKey(targetInventory));
            Assert.IsFalse(Connector(upper).ConnectedTargets.ContainsKey(targetInventory));

            world.RemoveBlock(upperPosition, BlockRemoveReason.ManualRemove);
            Assert.IsTrue(Connector(lower).ConnectedTargets.ContainsKey(targetInventory));
        }

        [TestCase(BlockDirection.North)]
        [TestCase(BlockDirection.East)]
        [TestCase(BlockDirection.South)]
        [TestCase(BlockDirection.West)]
        public void NonCentralHeightMismatchDoesNotConnect(BlockDirection direction)
        {
            CreateWorld();
            var world = ServerContext.WorldBlockDatastore;
            var targetPosition = Vector3Int.up + direction.ConvertLocalCell(Vector3Int.forward);
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltConveyorUp,
                Vector3Int.up, direction, Array.Empty<BlockCreateParam>(), out var source));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId,
                targetPosition, direction, Array.Empty<BlockCreateParam>(), out var target));
            Assert.IsFalse(Connector(source).ConnectedTargets.ContainsKey(
                target.GetComponent<VanillaBeltConveyorComponent>()));
        }

        private static void CreateWorld()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> Connector(IBlock block)
        {
            return block.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();
        }
    }
}
