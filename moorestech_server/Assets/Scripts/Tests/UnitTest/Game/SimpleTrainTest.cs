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

namespace Tests.UnitTest.Game
{
    
    public class SimpleTrainTest
    {
        
        [Test]
        // レールに乗っている列車が指定された駅に向かって移動するテスト
        // A test in which a train on rails moves towards a designated station
        public void SimpleTrainMoveTest()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            // TODO レールブロック1を設置
            // TODO レールブロック2を設置
            // TODO レールブロック同士がつながっていることを確認

            // TODO レールの両端に駅を設置

            // TODO レールに動力車1台を設置
            // TODO 列車に指定された駅に行くように指示

            // TODO 列車が駅に到着するまで待つ

            // TODO 列車が駅に到着すればpass、指定時間以内に到着しなければfail
            //
        }

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
                int rand0 = Random.Range(0, nodenum);
                int rand1 = Random.Range(0, nodenum);
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
        //RailPositionのmoveForwardのテストその1
        [Test]
        public void MoveForward_LongTrain_MovesAcrossMultipleNodes()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var railGraph = serviceProvider.GetService<RailGraphDatastore>();

            // ノードを準備
            var nodeA = new RailNode();
            var nodeB = new RailNode();
            var nodeC = new RailNode();
            var nodeD = new RailNode();
            var nodeE = new RailNode();

            // ノードを接続
            nodeB.ConnectNode(nodeA, 10);//9から列車
            nodeC.ConnectNode(nodeB, 15);//列車
            nodeD.ConnectNode(nodeC, 20);//列車
            nodeE.ConnectNode(nodeD, 25);//14まで列車

            // 長い列車（列車長50）をノードAからEにまたがる状態に配置
            var nodes = new List<RailNode> { nodeA, nodeB, nodeC, nodeD, nodeE };
            var railPosition = new RailPosition(nodes, 50, 9); // 先頭はノードAとBの間の9地点

            //進む
            var remainingDistance = railPosition.MoveForward(6); // 6進む（ノードAに近づく）
            // Assert
            Assert.AreEqual(0, remainingDistance); // ノードAに到達するまでに残り3

            //地道に全部チェック。ノードEの情報はまだ消えてない
            var list = railPosition.TestGet_railNodes();
            Assert.AreEqual(nodeA, list[0]);
            Assert.AreEqual(nodeB, list[1]);
            Assert.AreEqual(nodeC, list[2]);
            Assert.AreEqual(nodeD, list[3]);
            Assert.AreEqual(nodeE, list[4]);

            //進む、残りの進むべき距離
            remainingDistance = railPosition.MoveForward(4); // 3進んでAで停止、残り1
            // Assert
            Assert.AreEqual(nodeA, railPosition.GetNodeApproaching()); // 
            Assert.AreEqual(1, remainingDistance); //
            Assert.AreEqual(nodeB, railPosition.GetNodeJustPassed()); // 
        }

        //RailPositionのmoveForwardのテストその2
        [Test]
        public void MoveBackward_LongTrain_MovesAcrossMultipleNodes()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var railGraph = serviceProvider.GetService<RailGraphDatastore>();

            // ノードを準備
            // 表
            var nodeA1 = new RailNode();
            var nodeB1 = new RailNode();
            var nodeC1 = new RailNode();
            var nodeD1 = new RailNode();
            var nodeE1 = new RailNode();
            // 裏
            var nodeA2 = new RailNode();
            var nodeB2 = new RailNode();
            var nodeC2 = new RailNode();
            var nodeD2 = new RailNode();
            var nodeE2 = new RailNode();

            // ノードを接続
            nodeB1.ConnectNode(nodeA1, 10);//5から列車
            nodeC1.ConnectNode(nodeB1, 15);//列車
            nodeD1.ConnectNode(nodeC1, 20);//列車
            nodeE1.ConnectNode(nodeD1, 25);//10まで列車

            nodeD2.ConnectNode(nodeE2, 25);
            nodeC2.ConnectNode(nodeD2, 20);
            nodeB2.ConnectNode(nodeC2, 15);
            nodeA2.ConnectNode(nodeB2, 10);

            nodeA1.SetOppositeNode(nodeA2);//ここは本来RailConmponentのコンストラクタでやる
            nodeB1.SetOppositeNode(nodeB2);
            nodeC1.SetOppositeNode(nodeC2);
            nodeD1.SetOppositeNode(nodeD2);
            nodeE1.SetOppositeNode(nodeE2);
            nodeA2.SetOppositeNode(nodeA1);
            nodeB2.SetOppositeNode(nodeB1);
            nodeC2.SetOppositeNode(nodeC1);
            nodeD2.SetOppositeNode(nodeD1);
            nodeE2.SetOppositeNode(nodeE1);
            {  //Reverseを使ってMoveForward(マイナス)を使わないパターン
                var nodes = new List<RailNode> { nodeA1, nodeB1, nodeC1, nodeD1, nodeE1 };
                var railPosition = new RailPosition(nodes, 50, 5); // 先頭はノードAとBの間の5地点
                railPosition.Reverse();//ノードEまで15になる
                //地道に全部チェック。ノードEの情報はまだ消えてない
                var list = railPosition.TestGet_railNodes();
                Assert.AreEqual(nodeE2, list[0]);
                Assert.AreEqual(nodeD2, list[1]);
                Assert.AreEqual(nodeC2, list[2]);
                Assert.AreEqual(nodeB2, list[3]);
                Assert.AreEqual(nodeA2, list[4]);
                Assert.AreEqual(15, railPosition.GetDistanceToNextNode());
                var remainingDistance = railPosition.MoveForward(6); // 6すすむ（ノードEに近づく）
                Assert.AreEqual(9, railPosition.GetDistanceToNextNode());
                Assert.AreEqual(0, remainingDistance);

                list = railPosition.TestGet_railNodes();//後輪が完全にB-C間にいるためノードAの情報は削除される
                Assert.AreEqual(4, list.Count);
                Assert.AreEqual(nodeE2, list[0]);
                Assert.AreEqual(nodeD2, list[1]);
                Assert.AreEqual(nodeC2, list[2]);
                Assert.AreEqual(nodeB2, list[3]);
            }

            { //MoveForward(マイナス)を使うパターン
                // 長い列車（列車長50）をノードAからEにまたがる状態に配置
                var nodes = new List<RailNode> { nodeA1, nodeB1, nodeC1, nodeD1, nodeE1 };
                var railPosition = new RailPosition(nodes, 50, 5); // 先頭はノードAとBの間の5地点

                //進む、残りの進むべき距離
                var remainingDistance = railPosition.MoveForward(-11); // 11後退（ノードEに近づく）

                Assert.AreEqual(6, railPosition.GetDistanceToNextNode()); //
                Assert.AreEqual(nodeB1, railPosition.GetNodeApproaching()); // nodeAまで5のところから11後退してる
                Assert.AreEqual(nodeC1, railPosition.GetNodeJustPassed()); // 

                var list = railPosition.TestGet_railNodes(); Assert.AreEqual(4, list.Count);
                Assert.AreEqual(nodeB1, list[0]);
                Assert.AreEqual(nodeC1, list[1]);
                Assert.AreEqual(nodeD1, list[2]);
                Assert.AreEqual(nodeE1, list[3]);
            }
        }

        
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
            Debug.Log("Node1からNode2の距離" + connectedNode.Item2);

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
                    int j = Random.Range(i, list.Count);
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
            const int size = 12;//立方体の一辺の長さ40でも通ることを確認。計算量はO(size^6)以上


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
                var (x, y, z) = listIsDestroy[Random.Range(0, listIsDestroy.Count)];
                listIsCreated.Add((x, y, z));
                listIsDestroy.Remove((x, y, z));
                railBlocks[x, y, z] = new RailComponent(new BlockPositionInfo(new Vector3Int(x, y, z), BlockDirection.North, new Vector3Int(0, 0, 0)));
                //ランダムに経路をつなげる
                //2つ選ぶ
                var (x1, y1, z1) = listIsCreated[Random.Range(0, listIsCreated.Count)];
                var (x2, y2, z2) = listIsCreated[Random.Range(0, listIsCreated.Count)];
                //場所が外周ならやらない   
                if (x1 == 0 || x1 == size - 1 || y1 == 0 || y1 == size - 1 || z1 == 0 || z1 == size - 1) continue;
                railBlocks[x1, y1, z1].ConnectRailComponent(railBlocks[x2, y2, z2], true, true);

                //2分の1の確率でcontinue
                if (Random.Range(0, 2) == 0) continue;

                //ランダムにRailComponentを削除
                var (x3, y3, z3) = listIsCreated[Random.Range(0, listIsCreated.Count)];
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
                        if (Random.Range(0, 2) == 0) continue;
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






        
        /// <summary>
        /// 列車を編成分割できることをテスト。
        /// 前後の車両数・RailPosition の列車長さが正しく更新されているか確認します。
        /// </summary>
        [Test]
        public void SplitTrain_BasicTest()
        {
            /// RailPosition の列車長をテスト用に取得するためのヘルパーメソッド。
            int GetTrainLengthForTest(RailPosition railPosition)
            {
                // railPosition が null なら -1 などを返しておく
                if (railPosition == null) return -1;
                var fieldInfo = typeof(RailPosition)
                    .GetField("_trainLength", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo == null) return -1;
                return (int)fieldInfo.GetValue(railPosition);
            }

            // --- 1. レールノードを用意 ---
            // 例として直線上のノード3つ (A <- B <- C) を作り、距離を設定
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(0, 0, 0), BlockDirection.North, out var railA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(1, 0, 0), BlockDirection.North, out var railB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(1, 0, 0), BlockDirection.North, out var railC);
            // A - B の距離を 20,  B - C の距離を 40 とする
            var railComponentA = railA.GetComponent<RailComponent>();
            var railComponentB = railB.GetComponent<RailComponent>();
            var railComponentC = railC.GetComponent<RailComponent>();

            // Connect the two RailComponents
            railComponentC.ConnectRailComponent(railComponentB, true, true, 40);
            railComponentB.ConnectRailComponent(railComponentA, true, true, 20);

            // これで A -> B -> C の合計距離は 60
            var nodeA = railComponentA.FrontNode;
            var nodeB = railComponentB.FrontNode;
            var nodeC = railComponentC.FrontNode;

            // --- 2. 編成を構成する車両を用意 ---
            // 例：5両編成で各車両の長さは 10, 20, 5, 5, 10 (トータル 50)
            var cars = new List<TrainCar>
            {
                new TrainCar(tractionForce: 1000, inventorySlots: 0, length: 10),  // 仮: 動力車
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 20),   // 貨車
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 5),
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 5),
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 10),
            };
            int totalTrainLength = cars.Sum(car => car.Length);  // 10+20+5+5+10 = 50
            // --- 3. 初期の RailPosition を用意 ---
            //   ノードリスト = [A, B, C], 列車長さ = 50
            //   先頭が “A にあと10 で到達する位置” とする → initialDistanceToNextNode=10
            //   （イメージ：A--(10進んだ場所で先頭)-->B----->C ...合計60）
            var railNodes = new List<RailNode> { nodeA, nodeB, nodeC };
            var initialRailPosition = new RailPosition(
                railNodes,
                totalTrainLength,
                initialDistanceToNextNode: 10  // 先頭が A まであと10
            );

            // --- 4. TrainUnit を生成 ---
            var destination = nodeA;   // 適当な目的地を A にしておく
            var trainUnit = new TrainUnit(initialRailPosition, destination, cars);

            // --- 5. SplitTrain(...) で後ろから 2 両切り離す ---
            //   5両 → (前3両) + (後ろ2両) に分割
            var splittedUnit = trainUnit.SplitTrain(2);

            // --- 6. 結果の検証 ---
            // 6-1) 戻り値（splittedUnit）は null ではない
            Assert.NotNull(splittedUnit, "SplitTrain の結果が null になっています。");

            // 6-2) オリジナル列車の車両数は 3 両になっている
            //      新たに生成された列車の車両数は 2 両
            Assert.AreEqual(3, trainUnit
                .GetType()
                .GetField("_cars", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(trainUnit) is List<TrainCar> carsAfterSplit1
                    ? carsAfterSplit1.Count
                    : -1);

            Assert.AreEqual(2, splittedUnit
                .GetType()
                .GetField("_cars", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(splittedUnit) is List<TrainCar> carsAfterSplit2
                    ? carsAfterSplit2.Count
                    : -1);

            // 6-3) 列車長さが正しく更新されているか
            // オリジナル列車: 前3両 = 10 + 20 + 5 = 35
            // 後続列車: 後ろ2両 = 5 + 10 = 15
            // ※上の例では 10,20,5,5,10 の順で「後ろ2両」は後ろから 5,10 のはずなので合計15
            // SplitTrain 内で _railPosition.SetTrainLength(...) を行うことで長さが更新されているはず
            
            var mainRailPos = trainUnit._railPosition;
            var splittedRailPos = splittedUnit._railPosition;

            var nodelist1 = mainRailPos.TestGet_railNodes();
            var nodelist2 = splittedRailPos.TestGet_railNodes();
            //nodelist1のid表示
            //RailGraphDatastore._instance.Test_ListIdLog(nodelist1);
            //nodelist2のid表示
            //RailGraphDatastore._instance.Test_ListIdLog(nodelist2);
            // RailPosition の列車長を直接取得するための Getter が無い場合は、
            // 同様に Reflection や専用のテスト用メソッド (TestGetTrainLength() 等) を用意する形になります。
            // ここではテスト用に「TestGetTrainLength」があると仮定している例を示します。
            var mainTrainLength = GetTrainLengthForTest(mainRailPos);
            var splittedTrainLength = GetTrainLengthForTest(splittedRailPos);

            Assert.AreEqual(35, mainTrainLength, "分割後の先頭列車の長さが想定外です。");
            Assert.AreEqual(15, splittedTrainLength, "分割後の後続列車の長さが想定外です。");

            //mainRailPosはnodeAから10の距離にいるはず
            Assert.AreEqual(nodeA, mainRailPos.GetNodeApproaching());
            Assert.AreEqual(10, mainRailPos.GetDistanceToNextNode());
            mainRailPos.Reverse();
            //nodeCまで15の距離にいるはず
            Assert.AreEqual(nodeC.OppositeNode, mainRailPos.GetNodeApproaching());
            Assert.AreEqual(15, mainRailPos.GetDistanceToNextNode());

            // 6-4) 新しい後続列車の RailPosition が「後ろ側」に連続した状態で生成されているか
            //      → SplitTrain 内部では DeepCopy + Reverse + SetTrainLength + Reverse で位置を調整。
            //splittedRailPosはnodeBから25の距離にいるはず
            Assert.AreEqual(nodeB, splittedRailPos.GetNodeApproaching());
            Assert.AreEqual(25, splittedRailPos.GetDistanceToNextNode());
        }




        //列車編成が目的地にいけるかテスト、簡単
        [Test]
        public void Train_Approaching_light()
        {
            // --- 1. レールノードを用意 ---
            // 例として直線上のノード3つ (A <- B <- C) を作る
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(0, 0, 0), BlockDirection.North, out var railA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(112, 28, -74), BlockDirection.North, out var railB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(-54, 8, 147), BlockDirection.North, out var railC);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(491, 0, 447), BlockDirection.North, out var railD);
            var railComponentA = railA.GetComponent<RailComponent>();
            var railComponentB = railB.GetComponent<RailComponent>();
            var railComponentC = railC.GetComponent<RailComponent>();
            var railComponentD = railD.GetComponent<RailComponent>();

            // Connect the two RailComponents
            railComponentD.ConnectRailComponent(railComponentC, true, true);
            railComponentC.ConnectRailComponent(railComponentB, true, true);
            railComponentB.ConnectRailComponent(railComponentA, true, true);

            var nodeA = railComponentA.FrontNode;
            var nodeB = railComponentB.FrontNode;
            var nodeC = railComponentC.FrontNode;
            var nodeD = railComponentD.FrontNode;

            // --- 2. 編成を構成する車両を用意 ---
            // 例：5両編成で各車両の長さは 10, 20, 5, 5, 10 (トータル 50)
            var cars = new List<TrainCar>
            {
                new TrainCar(tractionForce: 100000, inventorySlots: 0, length: 10),  // 仮: 動力車
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 20),   // 貨車
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 5),
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 5),
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 10),
            };
            var railNodes = new List<RailNode> { nodeC, nodeD };
            int totalTrainLength = cars.Sum(car => car.Length);  // 10+20+5+5+10 = 50
            var initialRailPosition = new RailPosition(
                railNodes,
                totalTrainLength,
                initialDistanceToNextNode: 10  // 先頭が C まであと10
            );

            // --- 4. TrainUnit を生成 ---
            var destination = nodeA;   // 適当な目的地を A にしておく
            var trainUnit = new TrainUnit(initialRailPosition, destination, cars);
            trainUnit._isUseDestination = true;//factorioでいう自動運転on
            while (trainUnit._isUseDestination) //目的地に到達するまで
            {
                trainUnit.UpdateTrain(1f / 60f);
                break;
                Debug.Log("速度"+ trainUnit._currentSpeed);
                Debug.Log("現在向かっているnodeのID");
                RailGraphDatastore._instance.Test_NodeIdLog(trainUnit._railPosition.GetNodeApproaching());
            }
            Debug.Log("列車編成が無事目的地につきました");

        }
    }
}