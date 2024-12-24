using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Train.RailGraph;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEditor.Experimental.GraphView;
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

            var node0 = new RailNode(railGraphDatastore);
            var node1 = new RailNode(railGraphDatastore);
            var node2 = new RailNode(railGraphDatastore);
            var node3 = new RailNode(railGraphDatastore);
            node0.ConnectNode(node1, 1);
            node1.ConnectNode(node2, 1);
            node2.ConnectNode(node3, 1);

            //ダイクストラ法を実行 node0からnode3までの最短経路を求める
            var outListPath = railGraphDatastore.FindShortestPath(node0, node3);

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

            var node0 = new RailNode(railGraphDatastore);
            var node1 = new RailNode(railGraphDatastore);
            var node2 = new RailNode(railGraphDatastore);
            var node3 = new RailNode(railGraphDatastore);
            node0.ConnectNode(node1, 123);
            node0.ConnectNode(node2, 345);
            node1.ConnectNode(node3, 400);
            node2.ConnectNode(node3, 1);

            //ダイクストラ法を実行 node0からnode3までの最短経路を求める
            var outListPath = railGraphDatastore.FindShortestPath(node0, node3);

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
            Debug.Log(nodenum);

            RailNode[] nodeList = new RailNode[nodenum];
            for (int i = 0; i < nodenum; i++)
            {
                nodeList[i] = new RailNode(railGraphDatastore);
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
                var outListPath = railGraphDatastore.FindShortestPath(node_start, node_end);
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

            var nodeA = new RailNode(railGraphDatastore);
            var nodeB = new RailNode(railGraphDatastore);
            var nodeC1 = new RailNode(railGraphDatastore);
            var nodeC2 = new RailNode(railGraphDatastore);
            var nodeD1 = new RailNode(railGraphDatastore);
            var nodeD2 = new RailNode(railGraphDatastore);
            nodeA.ConnectNode(nodeC1, 3782);
            nodeB.ConnectNode(nodeC1, 67329);
            nodeC1.ConnectNode(nodeD1, 71894);
            nodeD2.ConnectNode(nodeC2, 17380);
            nodeC2.ConnectNode(nodeA, 28973);
            nodeC2.ConnectNode(nodeB, 718);

            //上から下
            //ダイクストラ法を実行 nodeAからnodeDまでの最短経路を求める
            var outListPath = railGraphDatastore.FindShortestPath(nodeA, nodeD1);

            //結果が正しいか
            Assert.AreEqual(3, outListPath.Count);
            Assert.AreEqual(nodeA, outListPath[0]);
            Assert.AreEqual(nodeC1, outListPath[1]);
            Assert.AreEqual(nodeD1, outListPath[2]);

            //下から上
            outListPath = railGraphDatastore.FindShortestPath(nodeD2, nodeA);

            //結果が正しいか
            Assert.AreEqual(3, outListPath.Count);
            Assert.AreEqual(nodeD2, outListPath[0]);
            Assert.AreEqual(nodeC2, outListPath[1]);
            Assert.AreEqual(nodeA, outListPath[2]);

            //AからBは繋がらない
            outListPath = railGraphDatastore.FindShortestPath(nodeA, nodeB);
            Assert.AreEqual(0, outListPath.Count);

            //ここでD1とD2を繋げると
            nodeD1.ConnectNode(nodeD2, 721);
            outListPath = railGraphDatastore.FindShortestPath(nodeA, nodeB);
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

            var nodeA = new RailNode(railGraphDatastore);
            var nodeB = new RailNode(railGraphDatastore);
            var nodeC = new RailNode(railGraphDatastore);

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
            var nodeA = new RailNode(railGraph);
            var nodeB = new RailNode(railGraph);
            var nodeC = new RailNode(railGraph);
            var nodeD = new RailNode(railGraph);
            var nodeE = new RailNode(railGraph);

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
            var nodeA1 = new RailNode(railGraph);
            var nodeB1 = new RailNode(railGraph);
            var nodeC1 = new RailNode(railGraph);
            var nodeD1 = new RailNode(railGraph);
            var nodeE1 = new RailNode(railGraph);
            // 裏
            var nodeA2 = new RailNode(railGraph);
            var nodeB2 = new RailNode(railGraph);
            var nodeC2 = new RailNode(railGraph);
            var nodeD2 = new RailNode(railGraph);
            var nodeE2 = new RailNode(railGraph);

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



    }
}