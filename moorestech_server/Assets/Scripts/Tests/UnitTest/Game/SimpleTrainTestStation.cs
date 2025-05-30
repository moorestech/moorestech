using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using Game.Train.RailGraph;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Block.Interface.Component;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using System.Collections.Generic;
using System;
using Tests.Module;
using static UnityEditor.Progress;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestStation
    {
        /// <summary>
        /// 駅の向きテスト
        /// 駅を設置したときにRailComponentが真ん中を通るようにならないといけない
        /// </summary>
        [Test]
        public void StationDirectionSimple()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 1) 駅をつくってrailcomponentの座標を確認
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, 0), BlockDirection.North, out var stationBlockA);

            // RailComponent を取得
            var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
            //2つあるかassert
            Assert.AreEqual(2, railcompos.RailComponents.Length);
            var railComponentA = railcompos.RailComponents[0];
            var railComponentB = railcompos.RailComponents[1];
            Debug.Log("railComponentAの座標" + railComponentA.Position);
            Debug.Log("railComponentBの座標" + railComponentB.Position);
        }

        [Test]
        public void StationDirectionMain()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //以下のを4方向でloopで確認する
            for (int i = 0; i < 4; i++)
            {
                var direction = (BlockDirection)4 + i;
                // 1) 駅をつくってrailcomponentの座標を確認
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 5 * i, 0), direction, out var stationBlockA);
                // RailComponent を取得
                var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
                //2つあるかassert
                Assert.AreEqual(2, railcompos.RailComponents.Length);
                var railComponentA = railcompos.RailComponents[0];
                var railComponentB = railcompos.RailComponents[1];
                Debug.Log("railComponentAの座標" + railComponentA.Position);
                Debug.Log("railComponentBの座標" + railComponentB.Position);
            }
        }

        /// <summary>
        /// 駅を2つつなげたときにrailNodeが自動でつながるか確認する
        /// </summary>
        [Test]
        public void StationConnectionSimple()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, 0), BlockDirection.North, out var stationBlockA);
            var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
            var railNodeA = railcompos.RailComponents[0].FrontNode;
            var railNodeB = railcompos.RailComponents[1].FrontNode;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(22, 0, 0), BlockDirection.North, out var stationBlockB);
            railcompos = stationBlockB.GetComponent<RailSaverComponent>();
            var railNodeC = railcompos.RailComponents[0].FrontNode;
            var railNodeD = railcompos.RailComponents[1].FrontNode;

            Debug.Log(
                RailGraphDatastore.GetDistanceBetweenNodes(railNodeA, railNodeB)
            );
            var length0 = RailGraphDatastore.GetDistanceBetweenNodes(railNodeB, railNodeC);
            Debug.Log(
                length0
            );
            Assert.AreEqual(0, length0);
            Debug.Log(
                RailGraphDatastore.GetDistanceBetweenNodes(railNodeC, railNodeD)
            );
        }
        /// <summary>
        /// 駅を2つつなげたときにrailNodeが自動でつながらないパターンを全探索で確認する
        /// ※全部やると50秒もかかるのでrandomにskipしている
        /// </summary>
        [Test]
        public void StationConnectionAllPattern()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //BlockDirection.Northからの4パターンで移動するx,y,zの量を計算で求める
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockSize;
            Vector3Int[] offsetvec3 = new Vector3Int[4];
            offsetvec3[0] = new Vector3Int(blockSize.x, 0, 0);//north
            offsetvec3[1] = new Vector3Int(0, 0, -blockSize.x);//east
            offsetvec3[2] = new Vector3Int(-blockSize.x, 0, 0);//south
            offsetvec3[3] = new Vector3Int(0, 0, blockSize.x);//west

            //※全部やると50秒もかかるのでrandomにskipしている
            //yは0、xとzで-blockSize.x～blockSize.xまで1マスずつ設置して一つだけ接続を満たすことを確認したい
            for (int dir = 0; dir < 4; dir++)
            {
                for (int i = -blockSize.x - 1; i <= blockSize.x + 1; i++)
                {
                    for (int j = -blockSize.x - 1; j <= blockSize.x + 1; j++)
                    {
                        if (i == 0 && j == 0) continue; // 自分の位置はスキップ
                        //ランダムにskipする
                        if (UnityEngine.Random.Range(0, 100) < 98) continue; // 98%の確率でスキップ
                        // 1) 駅をつくってrailcomponentの座標を確認
                        worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, 0), (BlockDirection)(dir + 4), out var stationBlockA);
                        var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
                        var railComponentA = railcompos.RailComponents[0];
                        var railComponentB = railcompos.RailComponents[1];
                        // 2) 駅をつくってrailcomponentの座標を確認
                        worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(i, 0, j), (BlockDirection)(dir + 4), out var stationBlockB);
                        railcompos = stationBlockB.GetComponent<RailSaverComponent>();
                        var railComponentC = railcompos.RailComponents[0];
                        var railComponentD = railcompos.RailComponents[1];
                        //接続されているか確認
                        var length = RailGraphDatastore.GetDistanceBetweenNodes(railComponentB.FrontNode, railComponentC.FrontNode, false);
                        if (new Vector3Int(i, 0, j) == offsetvec3[dir])
                        {
                            Assert.AreEqual(0, length);
                        }
                        else
                        {
                            Assert.AreNotEqual(0, length);
                        }
                        //接続されているか確認
                        length = RailGraphDatastore.GetDistanceBetweenNodes(railComponentD.FrontNode, railComponentA.FrontNode, false);
                        if (new Vector3Int(i, 0, j) == -offsetvec3[dir])
                        {
                            Assert.AreEqual(0, length);
                        }
                        else
                        {
                            Assert.AreNotEqual(0, length);
                        }
                        worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 0));
                        worldBlockDatastore.RemoveBlock(new Vector3Int(i, 0, j));
                    }
                }
            }
        }

        [Test]
        public void TrainStationItemInputOutputTest()
        {
            // テスト環境の初期化    
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 駅ブロックを設置    
            var stationPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, stationPosition, BlockDirection.North, out var stationBlock);
            Assert.IsNotNull(stationBlock, "駅ブロックの設置に失敗");

            // StationComponentの取得    
            var stationComponent = stationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(stationComponent, "StationComponentが見つからない");

            // アイテム搬入テスト用のチェストを設置    
            var inputChestPosition = new Vector3Int(4, 0, -1);
            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.ChestId,
                inputChestPosition,
                BlockDirection.North,
                out var inputChest
            );
            Assert.IsNotNull(inputChest, "搬入用チェストの設置に失敗");

            // アイテム搬出テスト用のチェストを設置    
            var outputChestPosition = new Vector3Int(6, 0, -1);
            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.ChestId,
                outputChestPosition,
                BlockDirection.North,
                out var outputChest
            );
            Assert.IsNotNull(outputChest, "搬出用チェストの設置に失敗");

            // アイテムをinputChestに挿入  
            var inputInventory = inputChest.GetComponent<IBlockInventory>();
            var testItem = ServerContext.ItemStackFactory.Create(new ItemId(1), 10);
            inputInventory.InsertItem(testItem);

            // アイテム搬入の確認    
            var insertedItem = inputInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), insertedItem.Id, "テストアイテムが正しく挿入されていない");
            Assert.AreEqual(10, insertedItem.Count, "アイテム数が正しくない");

            // 駅とチェスト間の接続を確立（BeltConveyorTestのパターンを参考）  これは自動で行われているはず
            var stationInventory = stationBlock.GetComponent<IBlockInventory>();
            var outputInventory = outputChest.GetComponent<IBlockInventory>();

            // 駅のコネクターを取得して出力チェストを接続  
            //var stationConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)stationBlock.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            //stationConnectInventory.Add(outputInventory, new ConnectedInfo());

            // inputChestから駅への接続も設定  
            //var inputConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)inputChest.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            //inputConnectInventory.Add(stationInventory, new ConnectedInfo());

            // アイテム搬送の待機（BeltConveyorInsertTestのパターンを参考）  
            var startTime = DateTime.Now;
            while (outputInventory.GetItem(0).Count == 0 && DateTime.Now - startTime < TimeSpan.FromSeconds(10))
            {
                GameUpdater.UpdateWithWait();
            }

            // 最終確認：outputChestにアイテムが移動したことを検証  
            var outputItem = outputInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), outputItem.Id, "出力チェストにアイテムが移動していない");
            Assert.IsTrue(outputItem.Count > 0, "出力チェストのアイテム数が0");

            // inputChestからアイテムが減っていることを確認  
            var remainingInputItem = inputInventory.GetItem(0);
            Assert.IsTrue(remainingInputItem.Count < 10, "入力チェストからアイテムが減っていない");
        }
    }
}