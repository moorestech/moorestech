using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.RailGraph;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using System.Collections.Generic;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestDijkstra
    {
        [Test]
        //ダイクストラ法が正しく動いているか 0-1-2-3
        public void DijkstraTest0()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //railGraphDatastoreに登録
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            var node0 = new RailNode();
            var node1 = new RailNode();
            var node2 = new RailNode();
            var node3 = new RailNode();
            node0.ConnectNode(node1, 1);
            node1.ConnectNode(node2, 1);
            node2.ConnectNode(node3, 1);

            //ダイクストラ法を実行 node0からnode3までの最短経路を求める
            var outListPath = RailGraphDatastore.FindShortestPath(node0, node3);

            //結果が正しいか
            Assert.AreEqual(4, outListPath.Count);
            Assert.AreEqual(node0, outListPath[0]);
            Assert.AreEqual(node1, outListPath[1]);
            Assert.AreEqual(node2, outListPath[2]);
            Assert.AreEqual(node3, outListPath[3]);
        }

        [Test]
        //ダイクストラ法が正しく動いているか、分岐あり 0=(1,2)=3
        public void DijkstraTest1()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //railGraphDatastoreに登録
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            var node0 = new RailNode();
            var node1 = new RailNode();
            var node2 = new RailNode();
            var node3 = new RailNode();
            node0.ConnectNode(node1, 123);
            node0.ConnectNode(node2, 345);
            node1.ConnectNode(node3, 400);
            node2.ConnectNode(node3, 1);

            //ダイクストラ法を実行 node0からnode3までの最短経路を求める
            var outListPath = RailGraphDatastore.FindShortestPath(node0, node3);

            //結果が正しいか
            Assert.AreEqual(3, outListPath.Count);
            Assert.AreEqual(node0, outListPath[0]);
            Assert.AreEqual(node2, outListPath[1]);
            Assert.AreEqual(node3, outListPath[2]);
        }


        [Test]
        //ダイクストラ法が正しく動いているか、複雑
        public void DijkstraTest2()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            //10000個のノードを作成し、それぞれが10つのノードにつながる。距離は1
            const int nodenum_powerexponent = 4;
            int nodenum = (int)System.Math.Pow(10, nodenum_powerexponent);

            RailNode[] nodeList = new RailNode[nodenum];
            for (int i = 0; i < nodenum; i++)
            {
                nodeList[i] = new RailNode();
            }
            //つながる規則は桁シフト(*10)して下位桁の数字を0-9とし、そのノードに対してつながる
            for (int i = 0; i < nodenum; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    var next = (i * 10) % nodenum + j;
                    nodeList[i].ConnectNode(nodeList[next], 1);
                }
            }

            //ダイクストラ法を実行、ランダムに。必ず距離4以内に任意のノードにつながるはず
            //例 1145から1419までの最短経路を求める
            //1145①→1451②
            //1451②→4514③
            //4514③→5141④
            //5141④→1419⑤
            int testnum = 1234;//1234567でも大丈夫なことを確認
            for (int i = 0; i < testnum; i++)
            {
                int rand0 = UnityEngine.Random.Range(0, nodenum);
                int rand1 = UnityEngine.Random.Range(0, nodenum);
                var node_start = nodeList[rand0];
                var node_end = nodeList[rand1];
                var outListPath = RailGraphDatastore.FindShortestPath(node_start, node_end);
                //結果が正しいか outListPathは4+1以内のはず
                if (outListPath.Count > 5)
                {
                    Debug.Log(rand0);
                    Debug.Log(rand1);
                }
                Assert.LessOrEqual(outListPath.Count, nodenum_powerexponent + 1);
            }
        }


        //50*50*50個のRailComponentの立方体の中にレールグラフを構築したり、内部だけくりぬいたりしてテスト。
        //ブロック大量設置してと思っていたがブロック設置が1000個で1秒以上かかるため断念
        //railComponentのみを大量設置する
        [Test]
        public void TestRailComponentsRandomCase()
        {
            //listを入力とし、順番をシャッフルする関数
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
            // Initialize the RailGraphDatastore
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();
            const int size = 8;//立方体の一辺の長さ40でも通ることを確認。計算量はO(size^6)以上


            //これから作るべきRailComponentの場所のリストの宣言
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
            //すでに作られているRailComponentのリスト
            var listIsCreated = new List<(int, int, int)>();
            var railBlocks = new RailComponent[size, size, size];


            while (listIsDestroy.Count != 0)
            {
                //ランダムにRailComponent作成
                var (x, y, z) = listIsDestroy[UnityEngine.Random.Range(0, listIsDestroy.Count)];
                listIsCreated.Add((x, y, z));
                listIsDestroy.Remove((x, y, z));
                railBlocks[x, y, z] = new RailComponent(new Vector3(x, y, z), BlockDirection.North, null);
                //ランダムに経路をつなげる
                //2つ選ぶ
                var (x1, y1, z1) = listIsCreated[UnityEngine.Random.Range(0, listIsCreated.Count)];
                var (x2, y2, z2) = listIsCreated[UnityEngine.Random.Range(0, listIsCreated.Count)];
                //場所が外周ならやらない   
                if (x1 == 0 || x1 == size - 1 || y1 == 0 || y1 == size - 1 || z1 == 0 || z1 == size - 1) continue;
                railBlocks[x1, y1, z1].ConnectRailComponent(railBlocks[x2, y2, z2], true, true);

                //2分の1の確率でcontinue
                if (UnityEngine.Random.Range(0, 2) == 0) continue;

                //ランダムにRailComponentを削除
                var (x3, y3, z3) = listIsCreated[UnityEngine.Random.Range(0, listIsCreated.Count)];
                railBlocks[x3, y3, z3].Destroy();
                listIsCreated.Remove((x3, y3, z3));
                listIsDestroy.Add((x3, y3, z3));
            }


            //自分から+1方向につなげていく。まずはランダムに
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        //2分の1の確率でcontinue
                        if (UnityEngine.Random.Range(0, 2) == 0) continue;
                        if (x < size - 1) railBlocks[x, y, z].ConnectRailComponent(railBlocks[x + 1, y, z], true, true);
                        if (y < size - 1) railBlocks[x, y, z].ConnectRailComponent(railBlocks[x, y + 1, z], true, true);
                        if (z < size - 1) railBlocks[x, y, z].ConnectRailComponent(railBlocks[x, y, z + 1], true, true);
                    }
                }
            }

            //残りを全部やる(全部順番にやるとランダムケースで起こるバグを拾えない可能性を考慮し)
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
            //この時点で
            //立方体の0,0,0から49,49,49まで経路があるか
            var node_s = railBlocks[0, 0, 0].FrontNode;
            var node_e = railBlocks[size - 1, size - 1, size - 1].FrontNode;

            //ダイクストラ法を実行 経路を求める
            var outListPath = RailGraphDatastore.FindShortestPath(node_s, node_e);
            // outListPathの長さが0でないことを確認
            Assert.AreNotEqual(0, outListPath.Count);

            //次に余分なpathを削除してちゃんと外周をたどるか
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

            //ダイクストラ
            outListPath = RailGraphDatastore.FindShortestPath(node_s, node_e);
            Assert.AreEqual(3 * (size - 1) + 1, outListPath.Count);
        }

    }
}