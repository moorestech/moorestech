using System;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection
{
    public class BeltConnectionCompatibilityTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SideInputUsesPortDirection(bool reverseOrder)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var sourcePos = Vector3Int.zero;
            var targetPos = Vector3Int.forward;
            if (reverseOrder) PlaceTarget();
            Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, sourcePos, BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var source));
            if (!reverseOrder) PlaceTarget();
            Assert.AreSame(world.World.GetBlock(targetPos), BeltEdgeTestWorld.Connector(source).ConnectedTargets.Single().Value.TargetBlock);

            #region Internal
            void PlaceTarget() => Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, targetPos,
                BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));
            #endregion
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChestAndBeltKeepBothDirections(bool reverseOrder)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            if (reverseOrder) PlaceChest();
            world.Place("UL", 1);
            if (!reverseOrder) PlaceChest();
            world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var target);
            var source = world.World.GetBlock(world.Position("UL"));
            var chest = world.World.GetBlock(Vector3Int.zero);
            Assert.IsTrue(BeltEdgeTestWorld.Connector(source).ConnectedTargets.Values.Any(info => ReferenceEquals(info.TargetBlock, chest)));
            Assert.IsTrue(BeltEdgeTestWorld.Connector(chest).ConnectedTargets.Values.Any(info => ReferenceEquals(info.TargetBlock, target)));

            #region Internal
            void PlaceChest() => Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            #endregion
        }

        [Test]
        public void SplitterParticipatesInUpperPriority()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var lower = world.Place("LL", 2);
            world.Place("UR", 1);
            Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.FilterSplitter, world.Position("UL"), BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var splitter));
            Assert.AreEqual(0, BeltEdgeTestWorld.Connector(lower).ConnectedTargets.Count);
            Assert.IsTrue(BeltEdgeTestWorld.Connector(splitter).ConnectedTargets.Values.Any(info => ReferenceEquals(info.TargetBlock, world.World.GetBlock(world.Position("UR")))));
            world.World.RemoveBlock(world.Position("UL"), BlockRemoveReason.ManualRemove);
            world.AssertEdges(new[] { "LL>UR" }, "splitter removed");
            world.Clear();
        }
    }
}
