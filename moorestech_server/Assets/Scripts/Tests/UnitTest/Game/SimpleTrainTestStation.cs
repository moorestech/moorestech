using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using Game.Train.RailGraph;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Block.Interface.Component;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestStation
    {

        //ブロック設置してrailComponentの表裏テスト
        [Test]
        public void TestRailComponentsAreConnected()
        {
            // Initialize the RailGraphDatastore
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(0, 0, 0), BlockDirection.North, out var rail1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(1, 0, 0), BlockDirection.North, out var rail2);

            //assert rail1が存在する
            Assert.NotNull(rail1, "Rail1 does not exist.");

            // Get two RailComponents
            var railComponent1 = rail1.GetComponent<RailComponent>();
            var railComponent2 = rail2.GetComponent<RailComponent>();

            // Connect the two RailComponents
            railComponent1.ConnectRailComponent(railComponent2, true, true); // Front of railComponent1 to front of railComponent2

            // Validate connections
            var connectedNodes = railComponent1.FrontNode.ConnectedNodesWithDistance;
            var connectedNode = connectedNodes.FirstOrDefault();

            Assert.NotNull(connectedNode, "RailComponent1 is not connected to RailComponent2.");
            Assert.AreEqual(railComponent2.FrontNode, connectedNode.Item1, "RailComponent1's FrontNode is not connected to RailComponent2's FrontNode.");
            //Assert.AreEqual(1, connectedNode.Item2, "The connection distance is not correct.");
            //Debug.Log("Node1からNode2の距離" + connectedNode.Item2);

            //表
            var outListPath = RailGraphDatastore.FindShortestPath(railComponent1.FrontNode, railComponent2.FrontNode);
            // outListPathの長さが0でないことを確認
            Assert.AreNotEqual(0, outListPath.Count);
            //裏
            outListPath = RailGraphDatastore.FindShortestPath(railComponent2.BackNode, railComponent2.BackNode);
            // outListPathの長さが0でないことを確認
            Assert.AreNotEqual(0, outListPath.Count);

        }


        /// <summary>
        /// 駅の向きテスト
        /// 駅を設置したときにRailComponentが真ん中を通るようにならないといけない
        /// </summary>
        [Test]
        public void StationDirectionSimple()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 1) 駅をつくってrailcomponentの座標を確認
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, 0), BlockDirection.North, out var stationBlockA);

            // RailComponent を取得
            var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
            //2つあるかassert
            Assert.AreEqual(2, railcompos.RailComponents.Length);
            var railComponentA = railcompos.RailComponents[0];
            var railComponentB = railcompos.RailComponents[1];
            Debug.Log("railComponentAの座標" + railComponentA.Position);
            Debug.Log("railComponentBの座標" + railComponentB.Position);
        }

        [Test]
        public void StationDirectionMain()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //以下のを4方向でloopで確認する

            for (int i = 0; i < 4; i++)
            {
                var direction = (BlockDirection)4 + i;
                // 1) 駅をつくってrailcomponentの座標を確認
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 5 * i, 0), direction, out var stationBlockA);
                // RailComponent を取得
                var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
                //2つあるかassert
                Assert.AreEqual(2, railcompos.RailComponents.Length);
                var railComponentA = railcompos.RailComponents[0];
                var railComponentB = railcompos.RailComponents[1];
                Debug.Log("railComponentAの座標" + railComponentA.Position);
                Debug.Log("railComponentBの座標" + railComponentB.Position);
            }
        }

        [Test]
        public void StationConnectionSimple()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, 0), BlockDirection.North, out var stationBlockA);
            var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
            var railNodeA = railcompos.RailComponents[0].FrontNode;
            var railNodeB = railcompos.RailComponents[1].FrontNode;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(22, 0, 0), BlockDirection.North, out var stationBlockB);
            railcompos = stationBlockB.GetComponent<RailSaverComponent>();
            var railNodeC = railcompos.RailComponents[0].FrontNode;
            var railNodeD = railcompos.RailComponents[1].FrontNode;

            Debug.Log(
                RailGraphDatastore.GetDistanceBetweenNodes(railNodeA, railNodeB)
            );
            var length0 = RailGraphDatastore.GetDistanceBetweenNodes(railNodeB, railNodeC);
            Debug.Log(
                length0
            );
            Assert.AreEqual(0, length0);
            Debug.Log(
                RailGraphDatastore.GetDistanceBetweenNodes(railNodeC, railNodeD)
            );
        }

    }
}
