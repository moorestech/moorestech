using System.Collections.Generic;
using Game.Train.RailGraph;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestRailPosition
    {
        //RailPositionのmoveForwardのテストその1
        [Test]
        public void MoveForward_LongTrain_MovesAcrossMultipleNodes()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var railGraphDatastore = env.GetRailGraphDatastore();

            // ノードを準備
            var nodeA = RailNode.CreateSingleAndRegister(railGraphDatastore);
            var nodeB = RailNode.CreateSingleAndRegister(railGraphDatastore);
            var nodeC = RailNode.CreateSingleAndRegister(railGraphDatastore);
            var nodeD = RailNode.CreateSingleAndRegister(railGraphDatastore);
            var nodeE = RailNode.CreateSingleAndRegister(railGraphDatastore);

            // ノードを接続
            nodeB.ConnectNode(nodeA, 10);//9から列車
            nodeC.ConnectNode(nodeB, 15);//列車
            nodeD.ConnectNode(nodeC, 20);//列車
            nodeE.ConnectNode(nodeD, 25);//14まで列車

            // 長い列車（列車長50）をノードAからEにまたがる状態に配置
            var nodes = new List<IRailNode> { nodeA, nodeB, nodeC, nodeD, nodeE };
            var railPosition = new RailPosition(nodes, 50, 9); // 先頭はノードAとBの間の9地点

            //進む
            var remainingDistance = railPosition.MoveForward(6); // 6進む（ノードAに近づく）
            // Assert
            Assert.AreEqual(6, remainingDistance, "ノードAまで進んだ際の残余距離が想定と一致していません。");

            //地道に全部チェック。ノードEの情報はまだ消えてない
            var list = railPosition.TestGet_railNodes();
            Assert.AreEqual(nodeA, list[0], "先頭ノードがnodeAではありません。");
            Assert.AreEqual(nodeB, list[1], "2番目のノードがnodeBではありません。");
            Assert.AreEqual(nodeC, list[2], "3番目のノードがnodeCではありません。");
            Assert.AreEqual(nodeD, list[3], "4番目のノードがnodeDではありません。");
            Assert.AreEqual(nodeE, list[4], "5番目のノードがnodeEではありません。");

            //進む、残りの進むべき距離
            remainingDistance = railPosition.MoveForward(4); // 3進んでAで停止、残り1
            // Assert
            Assert.AreEqual(nodeA, railPosition.GetNodeApproaching(), "進行後の接近先ノードがnodeAではありません。");
            Assert.AreEqual(3, remainingDistance, "ノードAで停止後の残余距離が3ではありません。");
            Assert.AreEqual(nodeB, railPosition.GetNodeJustPassed(), "通過済みノードがnodeBではありません。");
        }

        //RailPositionのmoveForwardのテストその2
        [Test]
        public void MoveBackward_LongTrain_MovesAcrossMultipleNodes()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var railGraphDatastore = env.GetRailGraphDatastore();

            // ノードを準備
            var (nodeA1, nodeA2) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (nodeB1, nodeB2) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (nodeC1, nodeC2) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (nodeD1, nodeD2) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (nodeE1, nodeE2) = RailNode.CreatePairAndRegister(railGraphDatastore);
            // ノードを接続
            nodeB1.ConnectNode(nodeA1, 10);//5から列車
            nodeC1.ConnectNode(nodeB1, 15);//列車
            nodeD1.ConnectNode(nodeC1, 20);//列車
            nodeE1.ConnectNode(nodeD1, 25);//10まで列車

            nodeD2.ConnectNode(nodeE2, 25);
            nodeC2.ConnectNode(nodeD2, 20);
            nodeB2.ConnectNode(nodeC2, 15);
            nodeA2.ConnectNode(nodeB2, 10);

            {  //Reverseを使ってMoveForward(マイナス)を使わないパターン
                var nodes = new List<IRailNode> { nodeA1, nodeB1, nodeC1, nodeD1, nodeE1 };
                var railPosition = new RailPosition(nodes, 50, 5); // 先頭はノードAとBの間の5地点
                railPosition.Reverse();//ノードEまで15になる
                //地道に全部チェック。ノードEの情報はまだ消えてない
                var list = railPosition.TestGet_railNodes();
                Assert.AreEqual(nodeE2, list[0], "反転後の先頭ノードがnodeE2ではありません。");
                Assert.AreEqual(nodeD2, list[1], "反転後の2番目のノードがnodeD2ではありません。");
                Assert.AreEqual(nodeC2, list[2], "反転後の3番目のノードがnodeC2ではありません。");
                Assert.AreEqual(nodeB2, list[3], "反転後の4番目のノードがnodeB2ではありません。");
                Assert.AreEqual(nodeA2, list[4], "反転後の5番目のノードがnodeA2ではありません。");
                Assert.AreEqual(15, railPosition.GetDistanceToNextNode(), "反転後の次ノードまでの距離が15ではありません。");
                var remainingDistance = railPosition.MoveForward(6); // 6すすむ（ノードEに近づく）
                Assert.AreEqual(9, railPosition.GetDistanceToNextNode(), "反転後に6進んだ際の残り距離が9ではありません。");
                Assert.AreEqual(6, remainingDistance, "反転後に6進んだ際の戻り値が6ではありません。");

                list = railPosition.TestGet_railNodes();//後輪が完全にB-C間にいるためノードAの情報は削除される
                Assert.AreEqual(4, list.Count, "反転後のノードリスト数が4ではありません。");
                Assert.AreEqual(nodeE2, list[0], "更新後の先頭ノードがnodeE2ではありません。");
                Assert.AreEqual(nodeD2, list[1], "更新後の2番目のノードがnodeD2ではありません。");
                Assert.AreEqual(nodeC2, list[2], "更新後の3番目のノードがnodeC2ではありません。");
                Assert.AreEqual(nodeB2, list[3], "更新後の4番目のノードがnodeB2ではありません。");
            }

            { //MoveForward(マイナス)を使うパターン
                // 長い列車（列車長50）をノードAからEにまたがる状態に配置
                var nodes = new List<IRailNode> { nodeA1, nodeB1, nodeC1, nodeD1, nodeE1 };
                var railPosition = new RailPosition(nodes, 50, 5); // 先頭はノードAとBの間の5地点

                //進む、残りの進むべき距離
                var remainingDistance = railPosition.MoveForward(-11); // 11後退（ノードEに近づく）

                Assert.AreEqual(6, railPosition.GetDistanceToNextNode(), "後退後の次ノードまでの距離が6ではありません。");
                Assert.AreEqual(nodeB1, railPosition.GetNodeApproaching(), "後退後の接近先ノードがnodeB1ではありません。");
                Assert.AreEqual(nodeC1, railPosition.GetNodeJustPassed(), "後退後の通過済みノードがnodeC1ではありません。");

                var list = railPosition.TestGet_railNodes(); Assert.AreEqual(4, list.Count, "後退後のノードリスト数が4ではありません。");
                Assert.AreEqual(nodeB1, list[0], "後退後の先頭ノードがnodeB1ではありません。");
                Assert.AreEqual(nodeC1, list[1], "後退後の2番目のノードがnodeC1ではありません。");
                Assert.AreEqual(nodeD1, list[2], "後退後の3番目のノードがnodeD1ではありません。");
                Assert.AreEqual(nodeE1, list[3], "後退後の4番目のノードがnodeE1ではありません。");
            }
        }

    }
}

