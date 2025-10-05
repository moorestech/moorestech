using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailGraph;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Component;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestStation
    {
        /// <summary>
        /// RailComponentを2つ設置し、手動で接続。FindShortestPathで接続されていることを確認
        /// </summary>
        [Test]
        public void TestRailComponentsAreConnected()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var railComponent1 = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railComponent2 = TrainTestHelper.PlaceRail(env, new Vector3Int(1, 0, 0), BlockDirection.North);

            Assert.NotNull(railComponent1, "Rail1 does not exist.");
            Assert.NotNull(railComponent2, "Rail2 does not exist.");

            railComponent1.ConnectRailComponent(railComponent2, true, true);

            var connectedNodes = railComponent1.FrontNode.ConnectedNodesWithDistance;
            var connectedNode = connectedNodes.FirstOrDefault();

            Assert.NotNull(connectedNode, "RailComponent1 is not connected to RailComponent2.");
            Assert.AreEqual(railComponent2.FrontNode, connectedNode.Item1, "RailComponent1's FrontNode is not connected to RailComponent2's FrontNode.");

            var path = RailGraphDatastore.FindShortestPath(railComponent1.FrontNode, railComponent2.FrontNode);
            Assert.AreNotEqual(0, path.Count);

            path = RailGraphDatastore.FindShortestPath(railComponent2.BackNode, railComponent2.BackNode);
            Assert.AreNotEqual(0, path.Count);
        }

        /// <summary>
        /// 駅ブロックの向きごとのRailComponent位置を検証
        /// </summary>
        [Test]
        public void StationDirectionSimple()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (stationBlockA, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                new Vector3Int(0, 0, 0),
                BlockDirection.North);
            Assert.IsNotNull(stationBlockA, "Station block placement failed");
            Assert.IsNotNull(railSaver, "RailSaverComponent is missing");

            Assert.AreEqual(2, railSaver.RailComponents.Length);
            var railComponentA = railSaver.RailComponents[0];
            var railComponentB = railSaver.RailComponents[1];
            Debug.Log("railComponentA Position: " + railComponentA.Position);
            Debug.Log("railComponentB Position: " + railComponentB.Position);
        }

        /// <summary>
        /// 駅ブロックの向きごとのRailComponent位置を検証
        /// Debug.logで位置を出力し、目視で確認する
        /// </summary>
        [Test]
        public void StationDirectionMain()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            for (int i = 0; i < 4; i++)
            {
                var direction = (BlockDirection)4 + i;
                var (stationBlockA, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                    env,
                    ForUnitTestModBlockId.TestTrainStation,
                    new Vector3Int(0, 5 * i, 0),
                    direction);
                Assert.IsNotNull(stationBlockA, "Station block placement failed");
                Assert.IsNotNull(railSaver, "RailSaverComponent is missing");

                Assert.AreEqual(2, railSaver.RailComponents.Length);
                var railComponentA = railSaver.RailComponents[0];
                var railComponentB = railSaver.RailComponents[1];
                Debug.Log("railComponentA Position: " + railComponentA.Position);
                Debug.Log("railComponentB Position: " + railComponentB.Position);
            }
        }

        [Test]
        public void StationConnectionSimple()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (_, firstSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                new Vector3Int(0, 0, 0),
                BlockDirection.North);
            var railNodeA = firstSaver.RailComponents[0].FrontNode;
            var railNodeB = firstSaver.RailComponents[1].FrontNode;

            var stationParam = (TrainStationBlockParam)MasterHolder.BlockMaster
                .GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockParam;
            var stationPosition = new Vector3Int(stationParam.StationDistance, 0, 0);

            var (_, secondSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                stationPosition,
                BlockDirection.North);
            var railNodeC = secondSaver.RailComponents[0].FrontNode;
            var railNodeD = secondSaver.RailComponents[1].FrontNode;

            Debug.Log(RailGraphDatastore.GetDistanceBetweenNodes(railNodeA, railNodeB));
            var length0 = RailGraphDatastore.GetDistanceBetweenNodes(railNodeB, railNodeC);
            Debug.Log(length0);
            Assert.AreEqual(0, length0);
            Debug.Log(RailGraphDatastore.GetDistanceBetweenNodes(railNodeC, railNodeD));
        }
    }
}
