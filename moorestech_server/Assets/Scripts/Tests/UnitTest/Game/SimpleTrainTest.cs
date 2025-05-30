using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using Game.Train.Train;
using Game.Train.RailGraph;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Train.Utility;
using Game.Block.Interface.Component;
using Core.Master;
using Game.PlayerInventory.Interface;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTest
    {
        
        //ブロック設置してrailComponentの表裏テスト
        [Test]
        public void TestRailComponentsAreConnected()
        {
            // Initialize the RailGraphDatastore
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
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

            //ダイクストラ法を実行 node000からnode494949までの最短経路を求める
            //表
            var outListPath = RailGraphDatastore.FindShortestPath(railComponent1.FrontNode, railComponent2.FrontNode);
            // outListPathの長さが0でないことを確認
            Assert.AreNotEqual(0, outListPath.Count);
            //裏
            outListPath = RailGraphDatastore.FindShortestPath(railComponent2.BackNode, railComponent2.BackNode);
            // outListPathの長さが0でないことを確認
            Assert.AreNotEqual(0, outListPath.Count);

        }

    }
}