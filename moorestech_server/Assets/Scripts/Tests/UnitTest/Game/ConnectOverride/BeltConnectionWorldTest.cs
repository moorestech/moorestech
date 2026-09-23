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
    public class BeltConnectionWorldTest
    {
        [Test]
        public void UpperCandidateDisplacesLowerAndRemovalRestoresIt()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var lower = new Vector3Int(0, 0, 0);
            var upper = new Vector3Int(0, 1, 0);
            var target = new Vector3Int(0, 1, 1);
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltConveyorUp, lower,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var lowerBlock);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, target,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var targetBlock);
            var targetInventory = targetBlock.GetComponent<SegmentBeltComponent>();
            var lowerConnector = InventoryConnector(lowerBlock);
            Assert.IsTrue(lowerConnector.ConnectedTargets.ContainsKey(targetInventory));

            world.TryAddBlock(ForUnitTestModBlockId.TestGearBeltConveyorDown, upper,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var upperBlock);
            var upperConnector = InventoryConnector(upperBlock);
            Assert.IsFalse(lowerConnector.ConnectedTargets.ContainsKey(targetInventory));
            Assert.IsTrue(upperConnector.ConnectedTargets.ContainsKey(targetInventory));

            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(10, 0, 10),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.IsFalse(lowerConnector.ConnectedTargets.ContainsKey(targetInventory));
            Assert.AreEqual(1, upperConnector.ConnectedTargets.Count);

            world.RemoveBlock(upper, BlockRemoveReason.ManualRemove);
            Assert.IsTrue(lowerConnector.ConnectedTargets.ContainsKey(targetInventory));
            Assert.AreEqual(0, upperConnector.ConnectedTargets.Count);
        }

        [Test]
        public void ForbiddenUpperPairDoesNotFallBackToLowerSource()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltConveyorUp, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var lower);
            world.TryAddBlock(ForUnitTestModBlockId.TestGearBeltConveyorUp, new Vector3Int(0, 1, 1),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var target);
            var inventory = target.GetComponent<SegmentBeltComponent>();
            Assert.IsTrue(InventoryConnector(lower).ConnectedTargets.ContainsKey(inventory));
            world.TryAddBlock(ForUnitTestModBlockId.TestGearBeltConveyorDown, new Vector3Int(0, 1, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var upper);
            Assert.IsFalse(InventoryConnector(lower).ConnectedTargets.ContainsKey(inventory));
            Assert.IsFalse(InventoryConnector(upper).ConnectedTargets.ContainsKey(inventory));
        }

        [Test]
        public void SplitterResolvesThreeOutputFacesIndependently()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var splitter);
            foreach (var (position, direction) in new[]
                     {
                         (Vector3Int.forward, BlockDirection.North),
                         (Vector3Int.right, BlockDirection.East),
                         (Vector3Int.left, BlockDirection.West)
                     })
            {
                world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position, direction,
                    Array.Empty<BlockCreateParam>(), out var target);
                Assert.IsTrue(InventoryConnector(splitter).ConnectedTargets.ContainsKey(
                    target.GetComponent<SegmentBeltComponent>()), direction.ToString());
            }
            Assert.AreEqual(3, InventoryConnector(splitter).ConnectedTargets.Count);
        }

        [Test]
        public void SelectedShapeFailureDoesNotFallBackToCompatibleLowerSource()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeUp, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var lower);
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeTarget, new Vector3Int(0, 1, 1),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var target);
            var inventory = target.GetComponent<SegmentBeltComponent>();
            Assert.IsTrue(InventoryConnector(lower).ConnectedTargets.ContainsKey(inventory));
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeDown, new Vector3Int(0, 1, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var upper);
            Assert.IsFalse(InventoryConnector(lower).ConnectedTargets.ContainsKey(inventory));
            Assert.IsFalse(InventoryConnector(upper).ConnectedTargets.ContainsKey(inventory));
            world.RemoveBlock(new Vector3Int(0, 1, 0), BlockRemoveReason.ManualRemove);
            Assert.IsTrue(InventoryConnector(lower).ConnectedTargets.ContainsKey(inventory));
        }
        [Test]
        public void IncompatibleUpperInputBlocksFallbackAndRemovalRestoresLowerInput()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var upperPosition = Vector3Int.up + Vector3Int.forward;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeDown, Vector3Int.up, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltConveyorDown, Vector3Int.forward, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var lower));
            var connector = InventoryConnector(source);
            var lowerInventory = lower.GetComponent<SegmentBeltComponent>();
            Assert.IsTrue(connector.ConnectedTargets.ContainsKey(lowerInventory));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeTarget, upperPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var upper));
            var upperInventory = upper.GetComponent<SegmentBeltComponent>();
            Assert.IsFalse(connector.ConnectedTargets.ContainsKey(lowerInventory));
            Assert.IsFalse(connector.ConnectedTargets.ContainsKey(upperInventory));
            Assert.IsTrue(world.RemoveBlock(upperPosition, BlockRemoveReason.ManualRemove));
            Assert.IsTrue(connector.ConnectedTargets.ContainsKey(lowerInventory));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestBeltShapeTarget, upperPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out upper));
            Assert.IsFalse(connector.ConnectedTargets.ContainsKey(lowerInventory));
            Assert.IsFalse(connector.ConnectedTargets.ContainsKey(upper.GetComponent<SegmentBeltComponent>()));
        }
        [Test]
        public void IneligibleOffsetSourceKeepsLegacyConnection()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltOffsetSource, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source);
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var target);
            Assert.IsTrue(InventoryConnector(source).ConnectedTargets.ContainsKey(
                target.GetComponent<SegmentBeltComponent>()));
        }

        [Test]
        public void IneligibleTargetKeepsLegacyLinkAndIsExcludedFromNewPriority()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltConveyorUp, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source);
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltUnrestrictedTarget,
                new Vector3Int(0, 1, 1), BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var unrestricted);
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltConveyorDown,
                new Vector3Int(0, 0, 1), BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var ruled);
            var connector = InventoryConnector(source);
            Assert.IsTrue(connector.ConnectedTargets.ContainsKey(
                unrestricted.GetComponent<SegmentBeltComponent>()));
            Assert.IsFalse(connector.ConnectedTargets.ContainsKey(
                ruled.GetComponent<SegmentBeltComponent>()));
            world.RemoveBlock(new Vector3Int(0, 1, 1), BlockRemoveReason.ManualRemove);
            Assert.AreEqual(0, connector.ConnectedTargets.Count);
        }

        [Test]
        public void DirectionalIneligibleUpperTargetDoesNotDisplaceEligibleLowerTarget()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var upper = new Vector3Int(0, 1, 1);
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 1, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source);
            // 入力は方向付き、出力offsetが非zeroなので対象外。
            // Its input has a direction; a nonzero output offset makes the target ineligible.
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltOffsetSource, upper,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var legacyTarget);
            world.TryAddBlock(ForUnitTestModBlockId.TestBeltConveyorDown, new Vector3Int(0, 0, 1),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var ruledTarget);
            var connector = InventoryConnector(source);
            var legacyInventory = legacyTarget.GetComponent<SegmentBeltComponent>();
            var ruledInventory = ruledTarget.GetComponent<SegmentBeltComponent>();
            Assert.IsTrue(connector.ConnectedTargets.ContainsKey(legacyInventory));
            Assert.IsTrue(connector.ConnectedTargets.ContainsKey(ruledInventory));
            world.RemoveBlock(upper, BlockRemoveReason.ManualRemove);
            Assert.IsFalse(connector.ConnectedTargets.ContainsKey(legacyInventory));
            Assert.IsTrue(connector.ConnectedTargets.ContainsKey(ruledInventory));
        }

        private static BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> InventoryConnector(IBlock block)
        {
            return block.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();
        }
    }
}
