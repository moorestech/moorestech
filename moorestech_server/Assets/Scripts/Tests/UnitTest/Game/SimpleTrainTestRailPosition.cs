using System.Collections.Generic;
using Game.Context;
using Game.Train.RailGraph;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestRailPosition
    {
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
            Assert.AreEqual(6, remainingDistance); // ノードAに到達するまでに残り3→進んだ距離は6

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
            Assert.AreEqual(3, remainingDistance); //
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
                Assert.AreEqual(6, remainingDistance);

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