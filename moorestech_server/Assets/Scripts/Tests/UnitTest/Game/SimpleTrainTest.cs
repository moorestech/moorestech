using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTest
    {
        [Test]
        public void DijkstraTest0()
        {
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            var node0 = new RailNode();
            var node1 = new RailNode();
            var node2 = new RailNode();
            var node3 = new RailNode();
            node0.ConnectNode(node1, 1);
            node1.ConnectNode(node2, 1);
            node2.ConnectNode(node3, 1);

            var outListPath = RailGraphDatastore.FindShortestPath(node0, node3);

            Assert.AreEqual(4, outListPath.Count, "経路に含まれるノード数が期待値と一致していません。");
            Assert.AreEqual(node0, outListPath[0], "最短経路の1番目のノードが始点ノードになっていません。");
            Assert.AreEqual(node1, outListPath[1], "最短経路の2番目のノードがnode1ではありません。");
            Assert.AreEqual(node2, outListPath[2], "最短経路の3番目のノードがnode2ではありません。");
            Assert.AreEqual(node3, outListPath[3], "最短経路の終点がnode3になっていません。");
        }

        [Test]
        public void DijkstraTest1()
        {
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            var node0 = new RailNode();
            var node1 = new RailNode();
            var node2 = new RailNode();
            var node3 = new RailNode();
            node0.ConnectNode(node1, 123);
            node0.ConnectNode(node2, 345);
            node1.ConnectNode(node3, 400);
            node2.ConnectNode(node3, 1);

            var outListPath = RailGraphDatastore.FindShortestPath(node0, node3);

            Assert.AreEqual(3, outListPath.Count, "最短経路のノード数が期待値と一致していません。");
            Assert.AreEqual(node0, outListPath[0], "最短経路の1番目のノードが始点ノードになっていません。");
            Assert.AreEqual(node2, outListPath[1], "コストの小さい経路が選択されていません (node2経由ではありません)。");
            Assert.AreEqual(node3, outListPath[2], "最短経路の終点がnode3になっていません。");
        }

        [Test]
        public void DijkstraTest2()
        {
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            const int nodenumPower = 4;
            int nodenum = (int)System.Math.Pow(10, nodenumPower);

            RailNode[] nodeList = new RailNode[nodenum];
            for (int i = 0; i < nodenum; i++)
            {
                nodeList[i] = new RailNode();
            }

            for (int i = 0; i < nodenum; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    var next = (i * 10) % nodenum + j;
                    nodeList[i].ConnectNode(nodeList[next], 1);
                }
            }

            int testnum = 1234;
            for (int i = 0; i < testnum; i++)
            {
                int rand0 = UnityEngine.Random.Range(0, nodenum);
                int rand1 = UnityEngine.Random.Range(0, nodenum);
                var nodeStart = nodeList[rand0];
                var nodeEnd = nodeList[rand1];
                var outListPath = RailGraphDatastore.FindShortestPath(nodeStart, nodeEnd);

                if (outListPath.Count > 5)
                {
                    Debug.Log(rand0);
                    Debug.Log(rand1);
                }
                Assert.LessOrEqual(outListPath.Count, nodenumPower + 1, "ランダム探索で得られた経路長が許容上限を超えています。");
            }
        }

        [Test]
        public void TestRailComponentsRandomCase()
        {
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            List<(int, int, int)> ShuffleList(List<(int, int, int)> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    int j = UnityEngine.Random.Range(i, list.Count);
                    var tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                }
                return list;
            }

            const int size = 12;
            var listIsDestroy = new List<(int, int, int)>();
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        listIsDestroy.Add((x, y, z));
                    }
                }
            }
            listIsDestroy = ShuffleList(listIsDestroy);

            var listIsCreated = new List<(int, int, int)>();
            var railBlocks = new RailComponent[size, size, size];

            while (listIsDestroy.Count != 0)
            {
                var (x, y, z) = listIsDestroy[UnityEngine.Random.Range(0, listIsDestroy.Count)];
                listIsCreated.Add((x, y, z));
                listIsDestroy.Remove((x, y, z));
                var railComponentId = new RailComponentID(new Vector3Int(x, y, z), 0);
                railBlocks[x, y, z] = new RailComponent(new Vector3(x, y, z), BlockDirection.North, railComponentId);

                var (x1, y1, z1) = listIsCreated[UnityEngine.Random.Range(0, listIsCreated.Count)];
                var (x2, y2, z2) = listIsCreated[UnityEngine.Random.Range(0, listIsCreated.Count)];
                if (x1 == 0 || x1 == size - 1 || y1 == 0 || y1 == size - 1 || z1 == 0 || z1 == size - 1) continue;
                railBlocks[x1, y1, z1].ConnectRailComponent(railBlocks[x2, y2, z2], true, true);

                if (UnityEngine.Random.Range(0, 2) == 0) continue;

                var (x3, y3, z3) = listIsCreated[UnityEngine.Random.Range(0, listIsCreated.Count)];
                railBlocks[x3, y3, z3].Destroy();
                listIsCreated.Remove((x3, y3, z3));
                listIsDestroy.Add((x3, y3, z3));
            }

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        if (x > 0) railBlocks[x, y, z].ConnectRailComponent(railBlocks[x - 1, y, z], false, false);
                        if (y > 0) railBlocks[x, y, z].ConnectRailComponent(railBlocks[x, y - 1, z], false, false);
                        if (z > 0) railBlocks[x, y, z].ConnectRailComponent(railBlocks[x, y, z - 1], false, false);
                    }
                }
            }

            var nodeStart = railBlocks[0, 0, 0].FrontNode;
            var nodeEnd = railBlocks[size - 1, size - 1, size - 1].FrontNode;
            var outListPath = RailGraphDatastore.FindShortestPath(nodeStart, nodeEnd);
            Assert.AreNotEqual(0, outListPath.Count, "ランダム生成されたレール構成で始点から終点までの経路が見つかりませんでした。");

            for (int x = 1; x < size - 1; x++)
            {
                for (int y = 1; y < size - 1; y++)
                {
                    for (int z = 1; z < size - 1; z++)
                    {
                        railBlocks[x, y, z].Destroy();
                    }
                }
            }

            outListPath = RailGraphDatastore.FindShortestPath(nodeStart, nodeEnd);
            Assert.AreEqual(3 * (size - 1) + 1, outListPath.Count, "経路遮断後の最短経路長が期待値と一致していません。");
        }

        [Test]
        public void Y_NodeCheck()
        {
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

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

            var outListPath = RailGraphDatastore.FindShortestPath(nodeA, nodeD1);
            Assert.AreEqual(3, outListPath.Count, "nodeAからnodeD1までの経路長が期待値と一致していません。");
            Assert.AreEqual(nodeA, outListPath[0], "nodeAからnodeD1までの経路の始点がnodeAになっていません。");
            Assert.AreEqual(nodeC1, outListPath[1], "nodeAからnodeD1までの経路の中継ノードがnodeC1ではありません。");
            Assert.AreEqual(nodeD1, outListPath[2], "nodeAからnodeD1までの経路の終点がnodeD1になっていません。");

            outListPath = RailGraphDatastore.FindShortestPath(nodeD2, nodeA);
            Assert.AreEqual(3, outListPath.Count, "nodeD2からnodeAまでの経路長が期待値と一致していません。");
            Assert.AreEqual(nodeD2, outListPath[0], "nodeD2からnodeAまでの経路の始点がnodeD2になっていません。");
            Assert.AreEqual(nodeC2, outListPath[1], "nodeD2からnodeAまでの経路の中継ノードがnodeC2ではありません。");
            Assert.AreEqual(nodeA, outListPath[2], "nodeD2からnodeAまでの経路の終点がnodeAになっていません。");

            outListPath = RailGraphDatastore.FindShortestPath(nodeA, nodeB);
            Assert.AreEqual(0, outListPath.Count, "閉路が存在しない状態でのnodeAからnodeBへの経路が空ではありません。");

            nodeD1.ConnectNode(nodeD2, 721);
            outListPath = RailGraphDatastore.FindShortestPath(nodeA, nodeB);
            Assert.AreEqual(6, outListPath.Count, "nodeAからnodeBへの経路長が期待値と一致していません。");
            Assert.AreEqual(nodeA, outListPath[0], "nodeAからnodeBへの経路の始点がnodeAになっていません。");
            Assert.AreEqual(nodeC1, outListPath[1], "nodeAからnodeBへの経路の2番目がnodeC1になっていません。");
            Assert.AreEqual(nodeD1, outListPath[2], "nodeAからnodeBへの経路の3番目がnodeD1になっていません。");
            Assert.AreEqual(nodeD2, outListPath[3], "nodeAからnodeBへの経路の4番目がnodeD2になっていません。");
            Assert.AreEqual(nodeC2, outListPath[4], "nodeAからnodeBへの経路の5番目がnodeC2になっていません。");
            Assert.AreEqual(nodeB, outListPath[5], "nodeAからnodeBへの経路の終点がnodeBになっていません。");
        }

        [Test]
        public void ConnectedNodesTest()
        {
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            var nodeA = new RailNode();
            var nodeB = new RailNode();
            var nodeC = new RailNode();

            nodeA.ConnectNode(nodeB, 10);
            nodeA.ConnectNode(nodeC, 20);

            var connectedNodes = nodeA.ConnectedNodes.ToList();

            Assert.AreEqual(2, connectedNodes.Count, "nodeAに接続されたノード数が期待値と一致していません。");
            Assert.IsTrue(connectedNodes.Contains(nodeB), "接続ノード一覧にnodeBが含まれていません。");
            Assert.IsTrue(connectedNodes.Contains(nodeC), "接続ノード一覧にnodeCが含まれていません。");
        }
    }
}
