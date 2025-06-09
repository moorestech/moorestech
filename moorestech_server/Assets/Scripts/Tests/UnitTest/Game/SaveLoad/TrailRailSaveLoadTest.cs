using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Train.RailGraph;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Core.Master;
using Game.Block.Interface.Component;
using Core.Update;
using System;

namespace Tests.UnitTest.Game.SaveLoad
{
    /// <summary>
    ///     RailComponentを1つをロードセーブするテスト
    /// </summary>
    public class TralRailSaveLoadTest
    {
        [Test]
        public void TralRailOneBlockSaveLoadTest()
        {
            // 1. DIコンテナ生成
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);

            // 2. 必要な参照を取得
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();

            // 3. ブロックマスタ上のRailBlockIdを用意
            // 4. レールブロック設置
            var pos = new Vector3Int(10, 0, 10);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, pos, BlockDirection.North, out var railBlock);

            // 5. RailSaverComponentを取得
            var railSaverComponent = railBlock.GetComponent<RailSaverComponent>();

            // 6. セーブ実行
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log("[RailComponentSaveLoadTest] SaveJson:\n" + json);

            // 7. 一旦ワールドからブロック削除(テスト用)
            worldBlockDatastore.RemoveBlock(pos);
            //接続情報もリセット
            RailGraphDatastore.ResetInstance();

            // 8. ロードのDIコンテナ再生成
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);
            var loadJson = loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;

            // 9. ロード
            loadJson.Load(json);

            // 10. ロード後にRailBlockを再取得
            var loadedRailBlock = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.IsNotNull(loadedRailBlock, "RailBlockが正しくロードされていません");

            // 11. セーブしたメモ文字列が一致しているか確認
            var loadedRailComp = loadedRailBlock.GetComponent<RailSaverComponent>();
            Assert.IsNotNull(loadedRailComp, "ロード後のRailComponentがnullです");

            var isdestroy = loadedRailComp.RailComponents[0].IsDestroy;
            Assert.AreEqual(false, isdestroy,
                "RailComponentが読み込まれませんでした");
        }




        //複数のRailComponentの接続情報を含むセーブロードテスト
        [Test]
        public void TralRailMultiBlockSaveLoadTest()
        {
            // 1. DIコンテナ生成
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);
            // 2. 必要な参照を取得
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            // 3. レールブロック設置
            const int num = 8;
            var AllRailComponents = new RailComponent[num];
            //4 場所をランダムに設定
            Vector3Int[] pos = new Vector3Int[num];
            for (int i = 0; i < num; i++)
            {
                pos[i] = new Vector3Int(UnityEngine.Random.Range(-100, 100), 0, UnityEngine.Random.Range(-100, 100));
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, pos[i], BlockDirection.North, out var railBlock);
                var railSaverComponent = railBlock.GetComponent<RailSaverComponent>();
                AllRailComponents[i] = railSaverComponent.RailComponents[0];
            }
            //5 適当に接続情報
            Dictionary<(int, int, bool, bool), bool> allconnect = new Dictionary<(int, int, bool, bool), bool>();
            for (int i = 0; i < num; i++)
            {
                for (int j = 0; j < num; j++)
                {
                    if (i == j) continue;
                    if (UnityEngine.Random.Range(0, 5) != 0) continue;
                    bool isFrontThis = UnityEngine.Random.Range(0, 2) == 0;
                    bool isFrontTarget = UnityEngine.Random.Range(0, 2) == 0;
                    AllRailComponents[i].ConnectRailComponent(AllRailComponents[j], isFrontThis, isFrontTarget);
                    allconnect[(i, j, isFrontThis, isFrontTarget)] = true;
                    allconnect[(j, i, !isFrontTarget, !isFrontThis)] = true;
                }
            }
            //6 セーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log("[RailComponentSaveLoadTest] SaveJson:\n" + json);
            //7 ワールドから削除
            for (int i = 0; i < num; i++)
            {
                worldBlockDatastore.RemoveBlock(pos[i]);
            }
            //接続情報もリセット
            RailGraphDatastore.ResetInstance();
            //8 ロード
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);
            var loadJson = loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            loadJson.Load(json);
            //9 ロード後にRailBlockを再取得
            for (int i = 0; i < num; i++)
            {
                var loadedRailBlock = ServerContext.WorldBlockDatastore.GetBlock(pos[i]);
                Assert.IsNotNull(loadedRailBlock, "RailBlockが正しくロードされていません");
                var loadedRailComp = loadedRailBlock.GetComponent<RailSaverComponent>();
                Assert.IsNotNull(loadedRailComp, "ロード後のRailComponentがnullです");
                AllRailComponents[i] = loadedRailComp.RailComponents[0];
            }

            //接続情報の確認
            for (int i = 0; i < num; i++)
            {
                for (int j = 0; j < num; j++)
                {
                    if (i == j) continue;
                    if (allconnect.ContainsKey((i, j, true, true)))
                    {
                        var nodes = AllRailComponents[i].FrontNode.ConnectedNodes;
                        //nodesの中にAllRailComponents[j]のFrontNodeがあるか
                        bool isconnect = false;
                        foreach (var node in nodes)
                        {
                            if (node == AllRailComponents[j].FrontNode)
                            {
                                isconnect = true;
                                break;
                            }
                        }
                        Assert.AreEqual(true, isconnect, "接続情報が正しくロードされていません");
                    }
                    if (allconnect.ContainsKey((i, j, true, false)))
                    {
                        var nodes = AllRailComponents[i].FrontNode.ConnectedNodes;
                        //nodesの中にAllRailComponents[j]のBackNodeがあるか
                        bool isconnect = false;
                        foreach (var node in nodes)
                        {
                            if (node == AllRailComponents[j].BackNode)
                            {
                                isconnect = true;
                                break;
                            }
                        }
                        Assert.AreEqual(true, isconnect, "接続情報が正しくロードされていません");
                    }
                    if (allconnect.ContainsKey((i, j, false, true)))
                    {
                        var nodes = AllRailComponents[i].BackNode.ConnectedNodes;
                        //nodesの中にAllRailComponents[j]のFrontNodeがあるか
                        bool isconnect = false;
                        foreach (var node in nodes)
                        {
                            if (node == AllRailComponents[j].FrontNode)
                            {
                                isconnect = true;
                                break;
                            }
                        }
                        Assert.AreEqual(true, isconnect, "接続情報が正しくロードされていません");
                    }
                    if (allconnect.ContainsKey((i, j, false, false)))
                    {
                        var nodes = AllRailComponents[i].BackNode.ConnectedNodes;
                        //nodesの中にAllRailComponents[j]のBackNodeがあるか
                        bool isconnect = false;
                        foreach (var node in nodes)
                        {
                            if (node == AllRailComponents[j].BackNode)
                            {
                                isconnect = true;
                                break;
                            }
                        }
                        Assert.AreEqual(true, isconnect, "接続情報が正しくロードされていません");
                    }

                }
            }
        }



        // 駅のインベントリにアイテムを入れた状態でセーブ・ロードを検証するテスト
        // 1 inputChestとstationを設置
        // 2 inputChestにアイテムを入れ、時間を進めてstationにアイテムが搬入されてセーブ
        // 3 ロード後にstationのインベントリにアイテムが入っていることを確認
        // 4 その後、outputChestを設置して、時間を進めてstationからアイテムが搬出されることを確認
        // これでstationのアイテム情報のセーブ・ロードの正しさ、ロード後の新規itemInventoryの接続に問題がないことがわかる
        [Test]
        public void TrainStationSaveLoadTest()
        {
            // 1. DIコンテナ生成
            var (_, diProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);

            // 2. 必要なサービス取得
            var blockFactory = ServerContext.BlockFactory;
            var blockStore = ServerContext.WorldBlockDatastore;
            var saveJsonAssembler = diProvider.GetService<AssembleSaveJsonText>();

            // 3. 駅ブロック設置
            var stationPos = new Vector3Int(0, 0, 0);
            blockStore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, stationPos, BlockDirection.North, out var stationBlock);
            Assert.IsNotNull(stationBlock, "駅ブロックの設置に失敗");

            var stationComponent = stationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(stationComponent, "StationComponentが見つからない");

            // 4. 入力チェスト設置・テストアイテム投入
            var inputChestPos = new Vector3Int(4, 0, -1);
            blockStore.TryAddBlock(ForUnitTestModBlockId.ChestId, inputChestPos, BlockDirection.North, out var inputChestBlock);

            var inputInventory = inputChestBlock.GetComponent<IBlockInventory>();
            var testItemStack = ServerContext.ItemStackFactory.Create(new ItemId(1), 10);
            inputInventory.InsertItem(testItemStack);

            var insertedStack = inputInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), insertedStack.Id, "テストアイテムの挿入に失敗");
            Assert.AreEqual(10, insertedStack.Count, "テストアイテム数が不一致");

            // 駅との接続は自動（BeltConveyorTestパターン）
            var stationInventory = stationBlock.GetComponent<IBlockInventory>();

            // 5. 搬送処理の進行
            for (int i = 0; i < 10; i++)
                GameUpdater.Update();

            // 入力チェストからアイテムが減っていることを確認
            var inputChestRemainder = inputInventory.GetItem(0);
            Assert.IsTrue(inputChestRemainder.Count < 10, "入力チェストのアイテムが減っていない");

            // 6. セーブ処理
            var saveJson = saveJsonAssembler.AssembleSaveJson();
            Debug.Log("[TrainStationSaveLoadTest] SaveJson:\n" + saveJson);

            // 7. ワールドから駅・入力チェスト削除
            blockStore.RemoveBlock(stationPos);
            blockStore.RemoveBlock(inputChestPos);
            RailGraphDatastore.ResetInstance();

            // 8. ロード処理
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);
            var worldLoader = loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            worldLoader.Load(saveJson);

            // 駅のインベントリにアイテムが存在することを確認
            blockStore = ServerContext.WorldBlockDatastore;
            var loadedStationBlock = blockStore.GetBlock(stationPos);
            Assert.IsNotNull(loadedStationBlock, "ロード後の駅ブロックが見つからない");

            var loadedStationComponent = loadedStationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(loadedStationComponent, "ロード後のStationComponentが見つからない");

            var loadedStationInventory = loadedStationBlock.GetComponent<IBlockInventory>();
            var stationStack = loadedStationInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), stationStack.Id, "ロード後の駅インベントリにアイテムが無い");
            Assert.IsTrue(stationStack.Count > 0, "ロード後の駅アイテム数が0");

            // 9. 出力チェスト設置
            var outputChestPos = new Vector3Int(6, 0, -1);
            blockStore.TryAddBlock(ForUnitTestModBlockId.ChestId, outputChestPos, BlockDirection.North, out var outputChestBlock);
            var outputInventory = outputChestBlock.GetComponent<IBlockInventory>();

            // 10. 駅→出力チェストへの搬送を進行
            for (int i = 0; i < 10; i++)
                GameUpdater.Update();

            // 11. 出力チェストへの搬出確認
            var outputStack = outputInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), outputStack.Id, "出力チェストにアイテムが移動していない");
            Assert.IsTrue(outputStack.Count > 0, "出力チェストのアイテム数が0");
        }

    }
}
