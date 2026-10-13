using System.Linq;
using Game.Block.Interface;
using Game.Train.SaveLoad;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainStation.Naming
{
    public class TrainStationConnectionPositionRemovalTest
    {
        [TestCase(0)]
        [TestCase(8)]
        public void RemovingEitherAdjacentStationPreservesSurvivorAndAllowsReplacement(int removedZ)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            Place(0);
            Place(8);
            var graph = environment.GetRailGraphDatastore();
            var positions = graph.GetRailPositionToConnectionDestination();
            Assert.AreEqual(3, positions.Count);
            Assert.AreEqual(1, positions.Values.Count(pair => !pair.first.IsDefault() && !pair.second.IsDefault()));

            // 共有位置のどちら側を撤去しても残存駅の接続先を保持する
            // Preserve the surviving station endpoint when either side of a shared position is removed
            Assert.IsTrue(environment.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, removedZ), BlockRemoveReason.ManualRemove));
            Assert.AreEqual(2, positions.Count);
            foreach (var pair in positions.Values)
            {
                Assert.AreNotEqual(pair.first.IsDefault(), pair.second.IsDefault());
                var destination = pair.first.IsDefault() ? pair.second : pair.first;
                Assert.AreEqual(8 - removedZ, destination.blockPosition.z);
                Assert.IsNotNull(graph.ResolveRailNode(destination));
            }

            // 再設置で共有位置の接続を復元し、双方の撤去後は索引を空にする
            // Restore the shared connection on replacement and empty the index after both stations are removed
            Place(removedZ);
            Assert.AreEqual(3, positions.Count);
            var shared = positions.Values.Single(pair => !pair.first.IsDefault() && !pair.second.IsDefault());
            var first = graph.ResolveRailNode(shared.first);
            var second = graph.ResolveRailNode(shared.second);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(0, graph.GetDistanceBetweenNodes(first, second.OppositeNode));
            Assert.IsTrue(environment.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove));
            Assert.IsTrue(environment.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 8), BlockRemoveReason.ManualRemove));
            Assert.IsEmpty(positions);

            #region Internal

            void Place(int z)
            {
                TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, z), BlockDirection.North);
            }

            #endregion
        }
    }
}
