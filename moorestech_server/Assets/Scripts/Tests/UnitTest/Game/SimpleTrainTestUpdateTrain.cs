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


namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestUpdateTrain
    {

        /// <summary>
        /// ループテスト
        /// </summary>

        [Test]
        public void LoopTrainTest()
        {
            // サーバーDIを立てて、WorldBlockDatastore や RailGraphDatastore を取得
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            // 1) ワールド上にいくつかレールを「TryAddBlock」して、RailComponentを取得
            //    例として4本だけ設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(0, 0, 0), BlockDirection.North, out var railBlockA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(2162, 2, -1667), BlockDirection.East, out var railBlockB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(-924, 12, 974), BlockDirection.West, out var railBlockC);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(1149, 0, 347), BlockDirection.South, out var railBlockD);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(33, 4, 334), BlockDirection.South, out var railBlockE);

            // RailComponent を取得
            var railComponentA = railBlockA.GetComponent<RailComponent>();
            var railComponentB = railBlockB.GetComponent<RailComponent>();
            var railComponentC = railBlockC.GetComponent<RailComponent>();
            var railComponentD = railBlockD.GetComponent<RailComponent>();
            var railComponentE = railBlockE.GetComponent<RailComponent>();

            // 2) レールどうしを Connect
            // D→C→B→A→D の順でつなげる
            //    defaultdistance=-1 ならばベジェ曲線長が自動計算される
            railComponentD.ConnectRailComponent(railComponentC, true, true, -1);
            railComponentC.ConnectRailComponent(railComponentB, true, true, -1);
            railComponentB.ConnectRailComponent(railComponentA, true, true, -1);
            railComponentA.ConnectRailComponent(railComponentD, true, true, -1);

            // ノード列を組み立てる
            // Aに向かっているという状況
            var nodeList = new List<RailNode>();
            nodeList.Add(railComponentA.FrontNode);
            nodeList.Add(railComponentB.FrontNode);
            nodeList.Add(railComponentC.FrontNode);
            nodeList.Add(railComponentD.FrontNode);
            nodeList.Add(railComponentA.FrontNode);

            // ここでは RailNode.GetDistanceToNode(...) をループで合計する例
            int totalDist = 0;
            for (int i = 0; i < nodeList.Count - 1; i++)
            {
                int dist = nodeList[i + 1].GetDistanceToNode(nodeList[i]);
                totalDist += dist;
            }
            nodeList.Add(railComponentB.FrontNode);

            // 列車の長さを適当にランダムに決めて計算
            // 列車をある距離進めて、反転して同じ距離進める。同じ場所にもどるはず
            for (int testnum = 0; testnum < 128; testnum++)
            {
                var rand = UnityEngine.Random.Range(0.001f, 0.9999f);
                int trainLength = (int)(totalDist * rand);
                if (trainLength < 1) trainLength = 1; //最低10

                // 4) RailPosition を作って先頭を配置
                //    initialDistanceToNextNode=5あたりから開始する例
                //nodeListのdeepcopy。これをしないといけないことに注意
                var nodeList2 = new List<RailNode>(nodeList);
                var railPosition = new RailPosition(nodeList2, trainLength, 5);

                // --- 4. TrainUnit を生成 ---
                var destination = railComponentE.FrontNode;//ありえない目的地をセットするが、ここではループするだけなので問題ない
                var cars = new List<TrainCar>
                {
                    new TrainCar(tractionForce: 600000, inventorySlots: 0, length: trainLength),  // 仮: 動力車
                };
                var trainUnit = new TrainUnit(railPosition, cars);

                // 5) 進めるtotal距離をランダムにきめる
                int totalrunDist = UnityEngine.Random.Range(1, 30000000);
                var remain = trainUnit.UpdateTrainByDistance(totalrunDist);
                //totalrunDist_は0でないといけない
                Assert.AreEqual(0, remain);
                //逆向きにして進みてみる
                trainUnit._railPosition.Reverse();
                remain = trainUnit.UpdateTrainByDistance(totalrunDist);
                Assert.AreEqual(0, remain);
                trainUnit._railPosition.Reverse();

                //逆向きにして同じ場所か
                //向かっているのがnodeAのはず
                Assert.AreEqual(railComponentA.FrontNode, railPosition.GetNodeApproaching());
                //initialDistanceToNextNode=5のはず
                Assert.AreEqual(5, railPosition.GetDistanceToNextNode());
            }
        }






        //列車編成が目的地にいけるかテスト、簡単
        [Test]
        public void Train_Approaching_light()
        {
            const bool DEBUG_LOG_FLAG = false;
            // --- 1. レールノードを用意 ---
            // 例として直線上のノード3つ (A <- B <- C) を作る
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //var railGraphDatastore = serviceProvider.GetService<RailGraphDatastore>();

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(0, 0, 0), BlockDirection.North, out var railA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(2162, 2, -1667), BlockDirection.North, out var railB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(-924, 12, 974), BlockDirection.North, out var railC);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, new Vector3Int(1149, 0, 347), BlockDirection.North, out var railD);
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
                new TrainCar(tractionForce: 600000, inventorySlots: 0, length: 80),  // 仮: 動力車
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 60),   // 貨車
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 65),
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 65),
                new TrainCar(tractionForce: 0, inventorySlots: 10, length: 60),
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
            var trainUnit = new TrainUnit(initialRailPosition, cars, destination);
            trainUnit.TurnOnAutoRun();
            int totaldist = 0;
            for (int i = 0; i < 65535; i++)//目的地に到達するまで→testフリーズは避けたいので有限で
            {
                int calceddist = trainUnit.UpdateTrainByTime(1f / 60f);
                totaldist += calceddist;
                if ((i % 60 == 0) & (DEBUG_LOG_FLAG))
                {
                    Debug.Log("列車速度" + trainUnit._currentSpeed);
                    Debug.Log("1フレームにすすむ距離int" + calceddist);
                    Debug.Log("現在向かっているnodeのID");
                    RailGraphDatastore._instance.Test_NodeIdLog(trainUnit._railPosition.GetNodeApproaching());
                }
            }

            Assert.AreEqual(nodeA, trainUnit._railPosition.GetNodeApproaching());
            Assert.AreEqual(0, trainUnit._railPosition.GetDistanceToNextNode());
            if (DEBUG_LOG_FLAG)
            {
                //Debug.Log("列車編成が無事目的地につきました");
            }

        }




        /// <summary>
        /// 駅から駅の列車運行テスト
        /// あとセーブ・ロードでつながったままかチェック
        /// </summary>
        [Test]
        public void StationTrainRun()
        {
            const bool DEBUG_LOG_FLAG = true;
            void RunTrain(TrainUnit trainUnit)
            {
                trainUnit.TurnOnAutoRun();//factorioでいう自動運転on
                //走行スタート
                int totaldist = 0;
                for (int i = 0; i < 65535; i++)//目的地に到達するまで→testフリーズは避けたいので有限で
                {
                    int calceddist = trainUnit.UpdateTrainByTime(1f / 60f);
                    totaldist += calceddist;
                    if ((i % 60 == 0) & (DEBUG_LOG_FLAG))
                    {
                        Debug.Log("列車速度" + trainUnit._currentSpeed);
                        Debug.Log("1フレームにすすむ距離int" + calceddist);
                        Debug.Log("現在向かっているnodeのID");
                        RailGraphDatastore._instance.Test_NodeIdLog(trainUnit._railPosition.GetNodeApproaching());
                    }
                    if (!trainUnit.IsAutoRun)
                    {
                        if (DEBUG_LOG_FLAG)
                        {
                            Debug.Log("" + i + "フレームでつきました。約" + (i / 60) + "秒");
                            Debug.Log("実装距離(int)" + totaldist + "");
                            Debug.Log("実装距離(world座標換算)" + ((float)totaldist / BezierUtility.RAIL_LENGTH_SCALE) + "");
                        }
                        break;
                    }
                }
            }
            /*
            railComponentAの座標(0.00, 0.50, 2.50)N
            railComponentBの座標(22.00, 0.50, 2.50)N
            railComponentAの座標(2.50, 5.50, 22.00)E
            railComponentBの座標(2.50, 5.50, 0.00)E
            railComponentAの座標(22.00, 10.50, 2.50)S
            railComponentBの座標(0.00, 10.50, 2.50)S
            railComponentAの座標(2.50, 15.50, 0.00)W
            railComponentBの座標(2.50, 15.50, 22.00)W
             */
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            RailComponent[] railComponentsData = new RailComponent[2 * 4 + 3];//1本の駅の入口と出口のrailcomponentを記憶、あと追加点
            BlockDirection[] blockDirections = new BlockDirection[] { BlockDirection.East, BlockDirection.North, BlockDirection.South, BlockDirection.West };
            Vector3Int[] dirarray = new Vector3Int[] { new Vector3Int(0, 0, -1), new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 1) };

            // 1) 駅を4つつくってrailcomponentの座標を確認
            // 駅1本:TestTrainStation+TestTrainCargoPlatform+TestTrainCargoPlatform+TestTrainCargoPlatform+TestTrainCargoPlatform+TestTrainStation
            // 22+11+11+11+11+22の構成
            for (int i = 0; i < 4; i++)
            {
                //yで重ならないよう調整、x,zは-1000～1000のランダム
                var position = new Vector3Int(UnityEngine.Random.Range(-1000, 1000), i * 6 + 3, UnityEngine.Random.Range(-1000, 1000));
                //気動車に対応する駅1
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, position, blockDirections[i], out var stationBlockA);
                //気動車に対応する駅2
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, position + dirarray[i] * 66, blockDirections[i], out var stationBlockB);
                var railcomposA = stationBlockA.GetComponent<RailSaverComponent>();
                var railcomposB = stationBlockB.GetComponent<RailSaverComponent>();
                //中間の貨物駅
                for (int j = 0; j < 4; j++)
                {
                    var offset11or22 = 22;
                    if (blockDirections[i] == BlockDirection.East) offset11or22 = 11;
                    if (blockDirections[i] == BlockDirection.South) offset11or22 = 11;
                    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainCargoPlatform, position + dirarray[i] * (offset11or22 + 11 * j), blockDirections[i], out var cargoblock);
                }
                Assert.AreEqual(2, railcomposA.RailComponents.Length);
                Assert.AreEqual(2, railcomposB.RailComponents.Length);
                var railComponentA = railcomposA.RailComponents[0];
                var railComponentB = railcomposB.RailComponents[1];
                railComponentsData[i * 2 + 0] = railComponentA;
                railComponentsData[i * 2 + 1] = railComponentB;
            }

            //駅をつなぐポイント 0-1に2点、1-2に1点、2-3に0点とする
            for (int i = 0; i < 3; i++)
            {
                //y=0,1,2で重ならないよう調整は-1000～1000のランダム
                var position = new Vector3Int(UnityEngine.Random.Range(-1000, 1000), i, UnityEngine.Random.Range(-1000, 1000));
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, position, BlockDirection.West, out var railBlockA);
                var railComponentA = railBlockA.GetComponent<RailSaverComponent>().RailComponents[0];
                railComponentsData[8 + i] = railComponentA;
            }

            railComponentsData[1].ConnectRailComponent(railComponentsData[8], true, true, -1);
            railComponentsData[8].ConnectRailComponent(railComponentsData[9], true, true, -1);
            railComponentsData[9].ConnectRailComponent(railComponentsData[2], true, true, -1);
            railComponentsData[3].ConnectRailComponent(railComponentsData[10], true, true, -1);
            railComponentsData[10].ConnectRailComponent(railComponentsData[4], true, true, -1);
            railComponentsData[5].ConnectRailComponent(railComponentsData[6], true, true, -1);
            //これで駅0→点8→点9→駅1→点10→駅2→駅3の順でつながった

            //ここから列車を走らせる
            var nodeList = new List<RailNode>();
            nodeList.Add(railComponentsData[9].FrontNode);
            nodeList.Add(railComponentsData[8].FrontNode);
            // 列車の長さは8～9の値しだい。とりあえず1
            var trainLength = 1;
            //RailPosition を作って先頭を配置
            //initialDistanceToNextNode=5あたりから開始する例
            //nodeListのdeepcopy。これをしないといけないことに注意
            var nodeList2 = new List<RailNode>(nodeList);
            var railPosition = new RailPosition(nodeList2, trainLength, 5);
            // --- TrainUnit を生成 ---
            var destination = railComponentsData[7].FrontNode;//目的地をセット
            var cars = new List<TrainCar>
            {
                new TrainCar(tractionForce: 600000, inventorySlots: 0, length: trainLength),  // 仮: 動力車
            };
            var trainUnit = new TrainUnit(railPosition, cars, destination);
            //走行スタート 現在地→駅3の終点
            RunTrain(trainUnit);
            Assert.AreEqual(railComponentsData[7].FrontNode, trainUnit._railPosition.GetNodeApproaching());
            Assert.AreEqual(0, trainUnit._railPosition.GetDistanceToNextNode());
            if (DEBUG_LOG_FLAG)
            {
                Debug.Log("列車編成が無事目的地につきました");
            }

            //走行スタート 駅3→駅0の終点
            trainUnit._railPosition.Reverse();
            trainUnit._destinationNode = railComponentsData[0].BackNode;
            RunTrain(trainUnit);
            Assert.AreEqual(railComponentsData[0].BackNode, trainUnit._railPosition.GetNodeApproaching());
            Assert.AreEqual(0, trainUnit._railPosition.GetDistanceToNextNode());
            if (DEBUG_LOG_FLAG)
            {
                Debug.Log("列車編成が無事目的地につきました");
            }
            //走行スタート 駅0→駅3の終点
            trainUnit._railPosition.Reverse();
            trainUnit._destinationNode = railComponentsData[7].FrontNode;
            RunTrain(trainUnit);
            Assert.AreEqual(railComponentsData[7].FrontNode, trainUnit._railPosition.GetNodeApproaching());
            Assert.AreEqual(0, trainUnit._railPosition.GetDistanceToNextNode());
            if (DEBUG_LOG_FLAG)
            {
                Debug.Log("列車編成が無事目的地につきました");
            }
        }


    }
}