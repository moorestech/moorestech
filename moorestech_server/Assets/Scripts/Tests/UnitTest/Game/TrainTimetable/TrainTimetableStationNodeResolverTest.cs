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
        [Test]
        public void ResolvesBackExitNodeOfTrainStation()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (block, _) = TrainTestHelper.PlaceBlockWithRailComponents(
                env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);

            var resolved = TrainTimetableStationNodeResolver.TryResolve(block, out var node);

            Assert.IsTrue(resolved);
            Assert.AreEqual(StationNodeSide.Back, node.StationRef.NodeSide);
            Assert.AreEqual(StationNodeRole.Exit, node.StationRef.NodeRole);
            Assert.AreSame(block, node.StationRef.StationBlock);
        }

        [Test]
        public void RejectsItemPlatform()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(
                env, ForUnitTestModBlockId.TestTrainItemPlatform, Vector3Int.zero, BlockDirection.North);

            Assert.IsFalse(TrainTimetableStationNodeResolver.TryResolve(block, out var node));
            Assert.IsNull(node);
        }

        [Test]
        public void RejectsNullBlock()
        {
            Assert.IsFalse(TrainTimetableStationNodeResolver.TryResolve(null, out var node));
            Assert.IsNull(node);
        }
    }
}
