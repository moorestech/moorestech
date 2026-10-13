using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainTimetableStationNodeResolverTest
    {
        [TestCase(StationNodeSide.Front)]
        [TestCase(StationNodeSide.Back)]
        public void ResolvesExitNodeOfRequestedSide(StationNodeSide side)
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (block, _) = TrainTestHelper.PlaceBlockWithRailComponents(
                env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);

            Assert.IsTrue(TrainTimetableStationNodeResolver.TryResolve(block, side, out var node));
            Assert.AreEqual(side, node.StationRef.NodeSide);
            Assert.AreEqual(StationNodeRole.Exit, node.StationRef.NodeRole);
            Assert.AreSame(block, node.StationRef.StationBlock);
        }

        [Test]
        public void RejectsItemPlatform()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(
                env, ForUnitTestModBlockId.TestTrainItemPlatform, Vector3Int.zero, BlockDirection.North);

            Assert.IsFalse(TrainTimetableStationNodeResolver.TryResolve(block, StationNodeSide.Back, out var node));
            Assert.IsNull(node);
        }

        [Test]
        public void RejectsNullBlock()
        {
            Assert.IsFalse(TrainTimetableStationNodeResolver.TryResolve(null, StationNodeSide.Back, out var node));
            Assert.IsNull(node);
        }
    }
}
