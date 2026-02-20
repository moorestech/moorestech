using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.RailCalc;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;


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
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            // 1) ワールド上にいくつかレールを「TryAddBlock」して、RailComponentを取得
            //    例として4本だけ設置
            var railBlockA = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railBlockB = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(2162, 2, -1667), BlockDirection.East);
            var railBlockC = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(-924, 12, 974), BlockDirection.West);
            var railBlockD = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(1149, 0, 347), BlockDirection.South);

            // RailComponent を取得
            var railComponentA = railBlockA.GetComponent<RailComponent>();
            var railComponentB = railBlockB.GetComponent<RailComponent>();
            var railComponentC = railBlockC.GetComponent<RailComponent>();
            var railComponentD = railBlockD.GetComponent<RailComponent>();

            // 2) レールどうしを Connect
            // D→C→B→A→D の順でつなげる
            //    defaultdistance=-1 ならばベジェ曲線長が自動計算される
            
            //railComponentD.ConnectRailComponent(railComponentC, true, true);
            railComponentD.FrontNode.ConnectNode(railComponentC.FrontNode);
            railComponentC.BackNode.ConnectNode(railComponentD.BackNode);
            //railComponentC.ConnectRailComponent(railComponentB, true, true);
            railComponentC.FrontNode.ConnectNode(railComponentB.FrontNode);
            railComponentB.BackNode.ConnectNode(railComponentC.BackNode);
            //railComponentB.ConnectRailComponent(railComponentA, true, true);
            railComponentB.FrontNode.ConnectNode(railComponentA.FrontNode);
            railComponentA.BackNode.ConnectNode(railComponentB.BackNode);
            //railComponentA.ConnectRailComponent(railComponentD, true, true);
            railComponentA.FrontNode.ConnectNode(railComponentD.FrontNode);
            railComponentD.BackNode.ConnectNode(railComponentA.BackNode);
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
            for (int testnum = 0; testnum < 1024; testnum++)
            {
                var rand = UnityEngine.Random.Range(0.0001f, 0.9999f);
                int trainLength = (int)(totalDist * rand);
                if (trainLength < 1) trainLength = 1; //最低10

                // 4) RailPosition を作って先頭を配置
                //    initialDistanceToNextNode=5あたりから開始する例
                //nodeListのdeepcopy。これをしないといけないことに注意
                var nodeList2 = new List<IRailNode>(nodeList);
                var railPosition = new RailPosition(nodeList2, trainLength, 5);

                // --- 4. TrainUnit を生成 ---
                var cars = new List<TrainCar>
                {
                    TrainTestCarFactory.CreateTrainCar(0, 600000, 0, trainLength, true),  // 仮: 動力車
                };

                // ここで Length を強制上書き（このテストだけ直書き）
                {
                    var f = typeof(TrainCar).GetField("<Length>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.NotNull(f, "TrainCar Length の backing field が見つかりません。実装が変わった可能性があります。");
                    f.SetValue(cars[0], trainLength);
                }

                var trainUnit = new TrainUnit(railPosition, cars, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

                // 5) 進めるtotal距離をランダムにきめる
                int totalrunDist = UnityEngine.Random.Range(1, 30000000);
                var remain = totalrunDist - trainUnit.UpdateTrainByDistance(totalrunDist);
                //totalrunDist_は0でないといけない
                Assert.AreEqual(0, remain, "往路の走行で残余距離が発生しています。");
                //逆向きにして進みてみる
                trainUnit.Reverse();
                remain = totalrunDist - trainUnit.UpdateTrainByDistance(totalrunDist);
                Assert.AreEqual(0, remain, "復路の走行で残余距離が発生しています。");
                trainUnit.Reverse();

                //逆向きにして同じ場所か
                //向かっているのがnodeAのはず
                Assert.AreEqual(railComponentA.FrontNode, railPosition.GetNodeApproaching(), "ループ走行後の接近先ノードが始点ノードに戻っていません。");
                //initialDistanceToNextNode=5のはず
                Assert.AreEqual(5, railPosition.GetDistanceToNextNode(), "ループ走行後の次ノードまでの距離が初期値と一致していません。");
            }
        }






        //列車編成が目的地にいけるかテスト、簡単
        [Test]
        public void Train_Approaching_light()
        {
            var debugLogFlag = false;
            // --- 1. レールノードを用意 ---
            // 例として直線上のノード3つ (A <- B <- C) を作る
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();
            var worldBlockDatastore = env.WorldBlockDatastore;

            var railA = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(2162, 2, -1667), BlockDirection.North);
            var railC = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(-924, 12, 974), BlockDirection.North);
            var railD = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(1149, 0, 347), BlockDirection.North);
            var railComponentA = railA.GetComponent<RailComponent>();
            var railComponentB = railB.GetComponent<RailComponent>();
            var railComponentC = railC.GetComponent<RailComponent>();
            var railComponentD = railD.GetComponent<RailComponent>();

            // Connect the two RailComponents
            //railComponentD.ConnectRailComponent(railComponentC, true, true);
            railComponentD.FrontNode.ConnectNode(railComponentC.FrontNode);
            railComponentC.BackNode.ConnectNode(railComponentD.BackNode);
            //railComponentC.ConnectRailComponent(railComponentB, true, true);
            railComponentC.FrontNode.ConnectNode(railComponentB.FrontNode);
            railComponentB.BackNode.ConnectNode(railComponentC.BackNode);
            //railComponentB.ConnectRailComponent(railComponentA, true, true);
            railComponentB.FrontNode.ConnectNode(railComponentA.FrontNode);
            railComponentA.BackNode.ConnectNode(railComponentB.BackNode);
            
            var nodeA = railComponentA.FrontNode;
            var nodeB = railComponentB.FrontNode;
            var nodeC = railComponentC.FrontNode;
            var nodeD = railComponentD.FrontNode;

            // --- 2. 編成を構成する車両を用意 ---
            // 例：5両編成で各車両の長さは 10, 20, 5, 5, 10 (トータル 50)
            var cars = new List<TrainCar>
            {
                TrainTestCarFactory.CreateTrainCar(0, 600000, 0, 80, true),  // 仮: 動力車
                TrainTestCarFactory.CreateTrainCar(1, 0, 10, 60, true),   // 貨車
                TrainTestCarFactory.CreateTrainCar(2, 0, 10, 65, true),
                TrainTestCarFactory.CreateTrainCar(3, 0, 10, 65, true),
                TrainTestCarFactory.CreateTrainCar(4, 0, 10, 60, true),
            };
            var railNodes = new List<IRailNode> { nodeC, nodeD };
            int totalTrainLength = cars.Sum(car => car.Length);  // 10+20+5+5+10 = 50
            var initialRailPosition = new RailPosition(
                railNodes,
                totalTrainLength,
                initialDistanceToNextNode: 10  // 先頭が C まであと10
            );

            // --- 4. TrainUnit を生成 ---
            var destination = nodeA;   // 目的地を A にしておく
            var trainUnit = new TrainUnit(initialRailPosition, cars, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
            trainUnit.trainDiagram.AddEntry(destination);
            trainUnit.TurnOnAutoRun();
            int totaldist = 0;
            var reachedDestination = false;
            int cnt = 0;//ループ助長カウント
            for (int i = 0; i < 65535; i++)//目的地に到達するまで→testフリーズは避けたいので有限で
            {
                var calceddist = trainUnit.Update();
                totaldist += calceddist;
                if ((i % 60 == 0) & (debugLogFlag))
                {
                    Debug.Log("列車速度" + trainUnit.CurrentSpeed);
                    Debug.Log("1フレームにすすむ距離int" + calceddist);
                    Debug.Log("現在向かっているnodeのID");
                    LogRailNodeId(trainUnit.RailPosition.GetNodeApproaching());
                }

                if (trainUnit.RailPosition.GetNodeApproaching() == destination &&
                    trainUnit.RailPosition.GetDistanceToNextNode() == 0)
                {
                    cnt++;
                    if (cnt == 3)
                    {
                        reachedDestination = true;
                        break;
                    }
                }
            }


            Assert.IsTrue(reachedDestination, "列車が目的地に到達できませんでした。");
            Assert.AreEqual(nodeA, trainUnit.RailPosition.GetNodeApproaching(), "自動運転後の接近先ノードが目的地nodeAになっていません。");
            Assert.AreEqual(0, trainUnit.RailPosition.GetDistanceToNextNode(), "自動運転後の目的地までの残距離が0ではありません。");
            if (debugLogFlag)
            {
                Debug.Log("列車編成が無事目的地につきました");
            }
        }

        /// <summary>
        /// 駅から駅の列車運行テスト
        /// あとセーブ・ロードでつながったままかチェック
        /// </summary>
        [Test]
        public void StationTrainRun()
        {
            var debugLogFlag = false;
            void RunTrain(TrainUnit trainUnit)
            {
                trainUnit.TurnOnAutoRun();//factorioでいう自動運転on
                //走行スタート
                int totaldist = 0;
                var reachedDestination = false;
                var framesElapsed = -1;
                for (int i = 0; i < 65535; i++)//目的地に到達するまで→testフリーズは避けたいので有限で
                {
                    int calceddist = trainUnit.Update();
                    totaldist += calceddist;
                    if ((i % 60 == 0) & (debugLogFlag))
                    {
                        Debug.Log("列車速度" + trainUnit.CurrentSpeed);
                        Debug.Log("1フレームにすすむ距離int" + calceddist);
                        Debug.Log("現在向かっているnodeのID");
                        LogRailNodeId(trainUnit.RailPosition.GetNodeApproaching());
                    }

                    if (trainUnit.RailPosition.GetNodeApproaching() == trainUnit.trainDiagram.GetCurrentNode() &&
                        trainUnit.RailPosition.GetDistanceToNextNode() == 0)
                    {
                        reachedDestination = true;
                        framesElapsed = i;
                        break;
                    }

                    if (!trainUnit.IsAutoRun)
                    {
                        reachedDestination = true;
                        framesElapsed = i;
                        break;
                    }
                }

                Assert.IsTrue(reachedDestination, "列車が目的地に到達できませんでした。");

                if (debugLogFlag && framesElapsed >= 0)
                {
                    Debug.Log("" + framesElapsed + "フレームでつきました。約" + (framesElapsed / 60) + "秒");
                    Debug.Log("実装距離(int)" + totaldist + "");
                    Debug.Log("実装距離(world座標換算)" + ((float)totaldist / BezierUtility.RAIL_LENGTH_SCALE) + "");
                }
            }

            const int LOOPCNT = 100;//2400で大丈夫だった
            for (int loopcnt = 0; loopcnt < LOOPCNT; loopcnt++)
            {
                var env = TrainTestHelper.CreateEnvironment();
                var worldBlockDatastore = env.WorldBlockDatastore;

                Vector3Int stationBlockSize = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockSize;
                Vector3Int cargoBBlockSize = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainCargoPlatform).BlockSize;
                Assert.AreNotEqual(0, stationBlockSize.x, "ブロックサイズが0");
                Assert.AreNotEqual(0, stationBlockSize.y, "ブロックサイズが0");
                Assert.AreNotEqual(0, stationBlockSize.z, "ブロックサイズが0");
                Assert.AreNotEqual(0, cargoBBlockSize.x, "ブロックサイズが0");
                Assert.AreNotEqual(0, cargoBBlockSize.y, "ブロックサイズが0");
                Assert.AreNotEqual(0, cargoBBlockSize.z, "ブロックサイズが0");

                RailComponent[] railComponentsData = new RailComponent[2 * 4 + 3];//1本の駅の入口と出口のrailcomponentを記憶、あと追加点
                BlockDirection[] blockDirections = new BlockDirection[] { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };
                Vector3Int[] dirarray = new Vector3Int[] { new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 0), new Vector3Int(0, 0, -1), new Vector3Int(-1, 0, 0) };

                // 1) 駅を4つつくってrailcomponentの座標を確認
                // 駅の列:TestTrainStation+TestTrainCargoPlatform+TestTrainCargoPlatform+TestTrainCargoPlatform+TestTrainCargoPlatform+TestTrainStation
                // 22+11+11+11+11+22の構成(旧)
                // 8+8+8+8+8+8の構成(新)
                int rand = UnityEngine.Random.Range(1000, 12000);
                if (UnityEngine.Random.Range(0, 3) == 0)
                    rand = UnityEngine.Random.Range(900, rand);
                if (UnityEngine.Random.Range(0, 3) == 0)
                    rand = UnityEngine.Random.Range(100, rand);
                for (int i = 0; i < 4; i++)
                {
                    //yで重ならないよう調整、x,zは-rand～randのランダム
                    var position = new Vector3Int(UnityEngine.Random.Range(-rand, rand), i * 6 + 3, UnityEngine.Random.Range(-rand, rand));
                    //気動車に対応する駅1
                    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, position, blockDirections[i], Array.Empty<BlockCreateParam>(), out var stationBlockA);
                    //気動車に対応する駅2
                    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, position + dirarray[i] * (stationBlockSize.z + 4 * cargoBBlockSize.z), blockDirections[i], Array.Empty<BlockCreateParam>(), out var stationBlockB);
                    var railcomposA = stationBlockA.GetComponents<RailComponent>();
                    var railcomposB = stationBlockB.GetComponents<RailComponent>();
                    //中間の貨物駅
                    for (int j = 0; j < 4; j++)
                    {
                        var offset11or22 = stationBlockSize.z;
                        if (blockDirections[i] == BlockDirection.West) offset11or22 = cargoBBlockSize.z;
                        if (blockDirections[i] == BlockDirection.South) offset11or22 = cargoBBlockSize.z;
                        worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainCargoPlatform, position + dirarray[i] * (offset11or22 + cargoBBlockSize.z * j), blockDirections[i], Array.Empty<BlockCreateParam>(), out var cargoblock);
                    }
                    Assert.AreEqual(2, railcomposA.Count, "駅Aに付随するRailComponent数が2本ではありません。");
                    Assert.AreEqual(2, railcomposB.Count, "駅Bに付随するRailComponent数が2本ではありません。");
                    var railComponentA = railcomposA[0];
                    var railComponentB = railcomposB[1];
                    railComponentsData[i * 2 + 0] = railComponentA;
                    railComponentsData[i * 2 + 1] = railComponentB;
                }

                //駅をつなぐポイント 0-1に2点、1-2に1点、2-3に0点とする
                for (int i = 0; i < 3; i++)
                {
                    //y=0,1,2で重ならないよう調整は-1000～1000のランダム
                    var position = new Vector3Int(UnityEngine.Random.Range(-1000, 1000), i, UnityEngine.Random.Range(-1000, 1000));
                    var railBlockA = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainRail, position, BlockDirection.West);
                    var railComponentA = railBlockA.GetComponent<RailComponent>();
                    railComponentsData[8 + i] = railComponentA;
                }
                
                //railComponentsData[1].ConnectRailComponent(railComponentsData[8], true, true);
                railComponentsData[1].FrontNode.ConnectNode(railComponentsData[8].FrontNode);
                railComponentsData[8].BackNode.ConnectNode(railComponentsData[1].BackNode);
                //railComponentsData[8].ConnectRailComponent(railComponentsData[9], true, true);
                railComponentsData[8].FrontNode.ConnectNode(railComponentsData[9].FrontNode);
                railComponentsData[9].BackNode.ConnectNode(railComponentsData[8].BackNode);
                //railComponentsData[9].ConnectRailComponent(railComponentsData[2], true, true);
                railComponentsData[9].FrontNode.ConnectNode(railComponentsData[2].FrontNode);
                railComponentsData[2].BackNode.ConnectNode(railComponentsData[9].BackNode);
                //railComponentsData[3].ConnectRailComponent(railComponentsData[10], true, true);
                railComponentsData[3].FrontNode.ConnectNode(railComponentsData[10].FrontNode);
                railComponentsData[10].BackNode.ConnectNode(railComponentsData[3].BackNode);
                //railComponentsData[10].ConnectRailComponent(railComponentsData[4], true, true);
                railComponentsData[10].FrontNode.ConnectNode(railComponentsData[4].FrontNode);
                railComponentsData[4].BackNode.ConnectNode(railComponentsData[10].BackNode);
                //railComponentsData[5].ConnectRailComponent(railComponentsData[6], true, true);
                railComponentsData[5].FrontNode.ConnectNode(railComponentsData[6].FrontNode);
                railComponentsData[6].BackNode.ConnectNode(railComponentsData[5].BackNode);
                //これで駅0→点8→点9→駅1→点10→駅2→駅3の順でつながった

                TrainTestHelper.Node2NodeCheckAndAssert(railComponentsData[0].FrontNode, railComponentsData[1].FrontNode, "駅0", "駅1");
                TrainTestHelper.Node2NodeCheckAndAssert(railComponentsData[2].FrontNode, railComponentsData[3].FrontNode, "駅2", "駅3");
                TrainTestHelper.Node2NodeCheckAndAssert(railComponentsData[6].FrontNode, railComponentsData[7].FrontNode, "駅6", "駅7");
                TrainTestHelper.Node2NodeCheckAndAssert(railComponentsData[4].FrontNode, railComponentsData[5].FrontNode, "駅4", "駅5");
                TrainTestHelper.Node2NodeCheckAndAssert(railComponentsData[0].FrontNode, railComponentsData[7].FrontNode, "駅0", "駅3");

                //ここから列車を走らせる
                var nodeList = new List<RailNode>();
                nodeList.Add(railComponentsData[9].FrontNode);
                nodeList.Add(railComponentsData[8].FrontNode);
                // 列車の長さは8～9の値しだい。とりあえず1
                var trainLength = 1;
                //RailPosition を作って先頭を配置
                //initialDistanceToNextNode=5あたりから開始する例
                //nodeListのdeepcopy。これをしないといけないことに注意
                var nodeList2 = new List<IRailNode>(nodeList);
                var railPosition = new RailPosition(nodeList2, trainLength, 5);
                // --- TrainUnit を生成 ---
                var destination = railComponentsData[7].FrontNode;//目的地をセット
                var cars = new List<TrainCar>
                {
                    TrainTestCarFactory.CreateTrainCar(0, 960000, 0, trainLength, true),  // 仮: 動力車まえ
                    TrainTestCarFactory.CreateTrainCar(1, 960000, 0, 0, false),  // 仮: 動力車うしろ
                };
                var trainUnit = new TrainUnit(railPosition, cars, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
                trainUnit.trainDiagram.AddEntry(destination);
                //走行スタート 現在地→駅3の終点
                RunTrain(trainUnit);
                Assert.AreEqual(railComponentsData[7].FrontNode, trainUnit.RailPosition.GetNodeApproaching(), "駅3到着後の接近先ノードが期待と一致していません。");
                Assert.AreEqual(0, trainUnit.RailPosition.GetDistanceToNextNode(), "駅3到着後の残距離が0ではありません。");
                if (debugLogFlag)
                {
                    Debug.Log("列車編成が無事目的地につきました1");
                }

                //走行スタート 駅3→駅0の終点
                trainUnit.Reverse();
                var secondDestination = railComponentsData[0].BackNode;
                trainUnit.trainDiagram.AddEntry(secondDestination);
                trainUnit.trainDiagram.MoveToNextEntry();
                RunTrain(trainUnit);
                Assert.AreEqual(railComponentsData[0].BackNode, trainUnit.RailPosition.GetNodeApproaching(), "駅0復路到着時の接近先ノードが期待と一致していません。");
                Assert.AreEqual(0, trainUnit.RailPosition.GetDistanceToNextNode(), "駅0復路到着時の残距離が0ではありません。");
                if (debugLogFlag)
                {
                    Debug.Log("列車編成が無事目的地につきました2");
                }
                //走行スタート 駅0→駅3の終点
                trainUnit.Reverse();
                var thirdDestination = railComponentsData[7].FrontNode;
                trainUnit.trainDiagram.AddEntry(thirdDestination);
                trainUnit.trainDiagram.MoveToNextEntry();
                RunTrain(trainUnit);
                Assert.AreEqual(railComponentsData[7].FrontNode, trainUnit.RailPosition.GetNodeApproaching(), "再度駅3へ到着した際の接近先ノードが期待と一致していません。");
                Assert.AreEqual(0, trainUnit.RailPosition.GetDistanceToNextNode(), "再度駅3へ到着した際の残距離が0ではありません。");
                if (debugLogFlag)
                {
                    Debug.Log("列車編成が無事目的地につきました3");
                }
            }
        }


        [Test]
        public void ComplexTrainTest()
        {
            //順列を作成し、順番をシャッフルする関数
            List<int> ReturnShuffleList(int n)
            {
                List<int> list = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    list.Add(i);
                }
                for (int i = 0; i < list.Count; i++)
                {
                    int j = UnityEngine.Random.Range(i, list.Count);
                    var tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                }
                return list;
            }

            var env = TrainTestHelper.CreateEnvironment();
            var worldBlockDatastore = env.WorldBlockDatastore;
            _ = env.GetRailGraphDatastore();

            //10000回のTryAddBlockし、それぞれが10つのRailComponentにつながる。距離は1
            const int nodenum_powerexponent = 3;//4でも確認済み
            int nodenum = (int)System.Math.Pow(10, nodenum_powerexponent);

            List<RailComponent> railComponents = new List<RailComponent>();
            //blockの位置をきめる
            //xの順列(0～nodenum)を作成、そのあとランダムシャッフルする
            var xlist = ReturnShuffleList(nodenum);
            var ylist = ReturnShuffleList(nodenum);
            var zlist = ReturnShuffleList(nodenum);
            for (int i = 0; i < nodenum; i++)
            {
                var railComponent = TrainTestHelper.PlaceRail(env, new Vector3Int(xlist[i], ylist[i], zlist[i]), BlockDirection.North);
                railComponents.Add(railComponent);
            }
            //これでnode生成はおわった。あとはつなげる。Bを作成→Aを作成→Bを削除という操作を行う


            //Bを作成
            Dictionary<(int, int, bool, bool), bool> connectB = new Dictionary<(int, int, bool, bool), bool>();//つながる元、つながる先、isFront_this,is_front_target
            const int blistnum = 1000;
            for (int i = 0; i < blistnum; i++)
            {
                int rand0 = UnityEngine.Random.Range(0, nodenum);
                int rand1 = UnityEngine.Random.Range(0, nodenum);
                bool isFront_this = UnityEngine.Random.Range(0, 2) == 0;
                bool isFront_target = UnityEngine.Random.Range(0, 2) == 0;
                if (rand0 == rand1) continue;
                if (connectB.ContainsKey((rand1, rand0, !isFront_target, !isFront_this))) continue;
                if (connectB.ContainsKey((rand0, rand1, isFront_this, isFront_target))) continue;
                connectB.Add((rand0, rand1, isFront_this, isFront_target), true);
            }
            //connectBを実行
            foreach (var key in connectB.Keys)
            {
                //railComponents[key.Item1].ConnectRailComponent(railComponents[key.Item2], key.Item3, key.Item4);
                var tmpn0 = key.Item3 ? railComponents[key.Item1].FrontNode : railComponents[key.Item1].BackNode;
                var tmpm1 = key.Item4 ? railComponents[key.Item2].FrontNode : railComponents[key.Item2].BackNode;
                tmpn0.ConnectNode(tmpm1);
                tmpm1.OppositeRailNode.ConnectNode(tmpn0.OppositeRailNode);
            }

            //Aを作成
            //つながる規則は桁シフト(*10)して下位桁の数字を0-9とし、そのノードに対してつながる
            for (int i = 0; i < nodenum; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    var next = (i * 10) % nodenum + j;
                    //railComponents[i].ConnectRailComponent(railComponents[next], true, true);
                    railComponents[i].FrontNode.ConnectNode(railComponents[next].FrontNode);
                    railComponents[next].BackNode.ConnectNode(railComponents[i].BackNode);
                }
            }

            //Bを一部または全部削除
            //ここではランダムに50%削除
            foreach (var key in connectB.Keys)
            {
                if (UnityEngine.Random.Range(0, 2) == 0)
                {
                    var next = (key.Item1 * 10) % nodenum / 10;
                    if ((key.Item2 / 10 == next) & (key.Item3) & (key.Item4)) continue;
                    //railComponents[key.Item1].DisconnectRailComponent(railComponents[key.Item2], key.Item3, key.Item4);
                    var tmpn0 = key.Item3 ? railComponents[key.Item1].FrontNode : railComponents[key.Item1].BackNode;
                    var tmpm1 = key.Item4 ? railComponents[key.Item2].FrontNode : railComponents[key.Item2].BackNode;
                    tmpn0.DisconnectNode(tmpm1);
                    tmpm1.OppositeRailNode.DisconnectNode(tmpn0.OppositeRailNode);
                }
            }


            ///////////////////////////////////////////////////////////////
            //あとは列車を走らせる
            const int DiagramNum = 100;//257で大丈夫なことを確認
            const int TestNum = 2;//1000で大丈夫なことを確認
            //複数の長さの列車を走らせる。短い～長い
            //列車を乗せるためのレールを新規に生成
            var railComponentStart = TrainTestHelper.PlaceRail(env, new Vector3Int(-100000, 0, 0), BlockDirection.South);
            //railComponentStart.ConnectRailComponent(railComponents[0], true, true);
            railComponentStart.FrontNode.ConnectNode(railComponents[0].FrontNode);
            railComponents[0].BackNode.ConnectNode(railComponentStart.BackNode);
            
            // ノード列を組み立てる
            // 列車がrailComponents[0]に向かっているという状況
            var nodeList = new List<RailNode>();
            nodeList.Add(railComponents[0].FrontNode);
            nodeList.Add(railComponentStart.FrontNode);

            // 列車の長さを適当にランダムに決めて計算
            for (int testnum = 0; testnum < TestNum; testnum++)
            {
                var trainLength = UnityEngine.Random.Range(1, 1000000);
                //RailPosition を作って先頭を配置
                //initialDistanceToNextNode=5あたりから開始する例
                //nodeListのdeepcopy。これをしないといけないことに注意
                var nodeList2 = new List<IRailNode>(nodeList);
                var railPosition = new RailPosition(nodeList2, trainLength, 5);

                // --- TrainUnit を生成 ---
                var cars = new List<TrainCar>
                {
                    TrainTestCarFactory.CreateTrainCar(0, 600000, 0, trainLength, true),  // 仮: 動力車
                };
                var trainUnit = new TrainUnit(railPosition, cars, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

                //進んで目的地についたら次の目的地をランダムにセット。100回繰り返し終了
                RailNode destination = null;
                for (int i = 0; i < DiagramNum; i++)
                {
                    var destinationid = UnityEngine.Random.Range(0, nodenum);
                    destination = railComponents[destinationid].FrontNode;//目的地をセット
                    trainUnit.trainDiagram.AddEntry(destination);
                }
                trainUnit.TurnOnAutoRun();//factorioでいう自動運転on

                var reachedDestination = false;
                for (int j = 0; j < 65535 * DiagramNum; j++)//目的地に到達するまで→testフリーズは避けたいので有限で
                {
                    trainUnit.Update();
                    if (j % 1 == 0)
                    {
                        //Debug.Log("列車速度" + trainUnit.CurrentSpeed);
                        //Debug.Log(trainUnit.RailPosition.GetDistanceToNextNode());
                    }
                    if (trainUnit.RailPosition.GetNodeApproaching() == destination)
                    {
                        reachedDestination = true;
                        break;
                    }
                    if (!trainUnit.IsAutoRun)
                    {
                        Debug.Log("自動運転がなぜかオフになりました");
                        reachedDestination = true;
                        break;
                    }
                }
                Assert.IsTrue(reachedDestination, "列車が目的地に到達できませんでした。");
            }
        }


        [Test]
        public void SplitTrain_BasicTest()
        {
            // RailPosition の列車長をテスト用に取得するためのヘルパーメソッド。
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
            var env = TrainTestHelper.CreateEnvironment();
            var worldBlockDatastore = env.WorldBlockDatastore;
            _ = env.GetRailGraphDatastore();

            var railComponentA = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railComponentB = TrainTestHelper.PlaceRail(env, new Vector3Int(1, 0, 0), BlockDirection.North);
            var railComponentC = TrainTestHelper.PlaceRail(env, new Vector3Int(1, 0, 0), BlockDirection.North);

            // Connect the two RailComponents
            railComponentC.FrontNode.ConnectNode(railComponentB.FrontNode, 40);
            railComponentB.BackNode.ConnectNode(railComponentC.BackNode, 40);
            railComponentB.FrontNode.ConnectNode(railComponentA.FrontNode, 20);
            railComponentA.BackNode.ConnectNode(railComponentB.BackNode, 20);

            // これで A -> B -> C の合計距離は 60
            var nodeA = railComponentA.FrontNode;
            var nodeB = railComponentB.FrontNode;
            var nodeC = railComponentC.FrontNode;

                        // レガシー長さを補正するヘルパー
            // Force legacy-length expectations for this historical scenario
            TrainCar ForceLegacyLength(TrainCar car, int legacyLength)
            {
                var lengthProperty = typeof(TrainCar).GetProperty(nameof(TrainCar.Length), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsNotNull(lengthProperty, "TrainCar.Length プロパティを取得できませんでした。");
                lengthProperty!.SetValue(car, legacyLength);
                return car;
            }

            // --- 2. 編成を構成する車両を用意 ---
            // 例として5両編成でそれぞれの長さを 10, 20, 5, 5, 10 (トータル 50)
            var cars = new List<TrainCar>
            {
                ForceLegacyLength(TrainTestCarFactory.CreateTrainCar(0, 1000, 0, 10, true), 10),  // 仮: 動力車
                ForceLegacyLength(TrainTestCarFactory.CreateTrainCar(1, 0, 10, 20, true), 20),   // 貨車
                ForceLegacyLength(TrainTestCarFactory.CreateTrainCar(2, 0, 10, 5, true), 5),
                ForceLegacyLength(TrainTestCarFactory.CreateTrainCar(3, 0, 10, 5, true), 5),
                ForceLegacyLength(TrainTestCarFactory.CreateTrainCar(4, 0, 10, 10, true), 10),
            };

            int totalTrainLength = cars.Sum(car => car.Length);  // 10+20+5+5+10 = 50
            // --- 3. 初期の RailPosition を用意 ---
            //   ノードリスト = [A, B, C], 列車長さ = 50
            //   先頭が “A にあと10 で到達する位置” とする → initialDistanceToNextNode=10
            //   （イメージ：A--(10進んだ場所で先頭)-->B----->C ...合計60）
            var railNodes = new List<IRailNode> { nodeA, nodeB, nodeC };
            var initialRailPosition = new RailPosition(
                railNodes,
                totalTrainLength,
                initialDistanceToNextNode: 10  // 先頭が A まであと10
            );

            // --- 4. TrainUnit を生成 ---
            var trainUnit = new TrainUnit(initialRailPosition, cars, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            // --- 5. SplitTrain(...) で後ろから 2 両切り離す ---
            //   5両 → (前3両) + (後ろ2両) に分割
            var splittedUnit = trainUnit.SplitTrain(2);

            // --- 6. 結果の検証 ---
            // 6-1) 戻り値（splittedUnit）は null ではない
            Assert.NotNull(splittedUnit, "SplitTrain の結果が null になっています。");

            // 6-2) オリジナル列車の車両数は 3 両になっている
            //      新たに生成された列車の車両数は 2 両
            Assert.AreEqual(3, trainUnit.Cars.Count, "分割後の先頭列車の車両数が3両ではありません。");

            Assert.AreEqual(2, splittedUnit.Cars.Count, "分割後に生成された後続列車の車両数が2両ではありません。");

            // 6-3) 列車長さが正しく更新されているか
            // オリジナル列車: 前3両 = 10 + 20 + 5 = 35
            // 後続列車: 後ろ2両 = 5 + 10 = 15
            // ※上の例では 10,20,5,5,10 の順で「後ろ2両」は後ろから 5,10 のはずなので合計15
            // SplitTrain 内で _railPosition.SetTrainLength(...) を行うことで長さが更新されているはず

            var mainRailPos = trainUnit.RailPosition;
            var splittedRailPos = splittedUnit.RailPosition;

            var nodelist1 = mainRailPos.TestGet_railNodes();
            var nodelist2 = splittedRailPos.TestGet_railNodes();
            //nodelist1のid表示
            //LogRailNodeIds(nodelist1);
            //nodelist2のid表示
            //LogRailNodeIds(nodelist2);
            // RailPosition の列車長を直接取得するための Getter が無い場合は、
            // 同様に Reflection や専用のテスト用メソッド (TestGetTrainLength() 等) を用意する形になります。
            // ここではテスト用に「TestGetTrainLength」があると仮定している例を示します。
            var mainTrainLength = GetTrainLengthForTest(mainRailPos);
            var splittedTrainLength = GetTrainLengthForTest(splittedRailPos);

            Assert.AreEqual(35, mainTrainLength, "分割後の先頭列車の長さが想定外です。");
            Assert.AreEqual(15, splittedTrainLength, "分割後の後続列車の長さが想定外です。");

            //mainRailPosはnodeAから10の距離にいるはず
            Assert.AreEqual(nodeA, mainRailPos.GetNodeApproaching(), "先頭列車の接近先ノードがnodeAではありません。");
            Assert.AreEqual(10, mainRailPos.GetDistanceToNextNode(), "先頭列車の次ノードまでの距離が10になっていません。");
            mainRailPos.Reverse();
            //nodeCまで15の距離にいるはず
            Assert.AreEqual(nodeC.OppositeNode, mainRailPos.GetNodeApproaching(), "反転後の先頭列車の接近先ノードがnodeCの反対側ではありません。");
            Assert.AreEqual(15, mainRailPos.GetDistanceToNextNode(), "反転後の先頭列車の次ノードまでの距離が15になっていません。");

            // 6-4) 新しい後続列車の RailPosition が「後ろ側」に連続した状態で生成されているか
            //      → SplitTrain 内部では DeepCopy + Reverse + SetTrainLength + Reverse で位置を調整。
            //splittedRailPosはnodeBから25の距離にいるはず
            Assert.AreEqual(nodeB, splittedRailPos.GetNodeApproaching(), "切り離した後続列車の接近先ノードがnodeBではありません。");
            Assert.AreEqual(25, splittedRailPos.GetDistanceToNextNode(), "切り離した後続列車の次ノードまでの距離が25になっていません。");
        }


        private static void LogRailNodeId(IRailNode node)
        {
            if (node == null)
                return;
            Debug.Log(node.NodeId);
        }


        private static void LogRailNodeIds(IEnumerable<RailNode> nodes)
        {
            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                LogRailNodeId(node);
            }
        }


    }
}

