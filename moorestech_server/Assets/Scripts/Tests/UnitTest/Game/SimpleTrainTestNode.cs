using System.Linq;
using Game.Context;
using Game.Train.RailGraph;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestNode
    {
        [Test]
        //Yの字の形に設置して、ノードが正しくつながっているかチェック
        public void Y_NodeCheck()
        {
            //Notionの図を参照
            //Yの字の左上がA、右上がB、真ん中がC1とC2、下がD1とD2
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //railGraphDatastoreに登録
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            var nodeA = new RailNode();
            var nodeB = new RailNode();
            var nodeC1 = new RailNode();
            var nodeC2 = new RailNode();
            var nodeD1 = new RailNode();
            var nodeD2 = new RailNode();
            nodeA.ConnectNode(nodeC1, 3782);
            nodeB.ConnectNode(nodeC1, 67329);
            nodeC1.ConnectNode(nodeD1, 71894);
            nodeD2.ConnectNode(nodeC2, 17380);
            nodeC2.ConnectNode(nodeA, 28973);
            nodeC2.ConnectNode(nodeB, 718);

            //上から下
            //ダイクストラ法を実行 nodeAからnodeDまでの最短経路を求める
            var outListPath = RailGraphDatastore.FindShortestPath(nodeA, nodeD1);

            //結果が正しいか
            Assert.AreEqual(3, outListPath.Count);
            Assert.AreEqual(nodeA, outListPath[0]);
            Assert.AreEqual(nodeC1, outListPath[1]);
            Assert.AreEqual(nodeD1, outListPath[2]);

            //下から上
            outListPath = RailGraphDatastore.FindShortestPath(nodeD2, nodeA);

            //結果が正しいか
            Assert.AreEqual(3, outListPath.Count);
            Assert.AreEqual(nodeD2, outListPath[0]);
            Assert.AreEqual(nodeC2, outListPath[1]);
            Assert.AreEqual(nodeA, outListPath[2]);

            //AからBは繋がらない
            outListPath = RailGraphDatastore.FindShortestPath(nodeA, nodeB);
            Assert.AreEqual(0, outListPath.Count);

            //ここでD1とD2を繋げると
            nodeD1.ConnectNode(nodeD2, 721);
            outListPath = RailGraphDatastore.FindShortestPath(nodeA, nodeB);
            Assert.AreEqual(6, outListPath.Count);
            Assert.AreEqual(nodeA, outListPath[0]);
            Assert.AreEqual(nodeC1, outListPath[1]);
            Assert.AreEqual(nodeD1, outListPath[2]);
            Assert.AreEqual(nodeD2, outListPath[3]);
            Assert.AreEqual(nodeC2, outListPath[4]);
            Assert.AreEqual(nodeB, outListPath[5]);
        }



        //RailGraphDatastoreに実装したGetConnectedNodesのテスト
        [Test]
        public void ConnectedNodesTest()
        {
            //Yの字の左上がA、右上がB、真ん中がC1とC2、下がD1とD2
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //railGraphDatastoreに登録
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            var nodeA = new RailNode();
            var nodeB = new RailNode();
            var nodeC = new RailNode();

            nodeA.ConnectNode(nodeB, 10);
            nodeA.ConnectNode(nodeC, 20);

            var connectedNodes = nodeA.ConnectedNodes.ToList();

            Assert.AreEqual(2, connectedNodes.Count);
            Assert.IsTrue(connectedNodes.Contains(nodeB));
            Assert.IsTrue(connectedNodes.Contains(nodeC));
        }
    }
}