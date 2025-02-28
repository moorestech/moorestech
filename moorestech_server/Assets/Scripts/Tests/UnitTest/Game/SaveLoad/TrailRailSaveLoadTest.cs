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
                pos[i] = new Vector3Int(Random.Range(-100, 100), 0, Random.Range(-100, 100));
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
                    if (Random.Range(0, 5) != 0) continue;
                    bool isFrontThis = Random.Range(0, 2) == 0;
                    bool isFrontTarget = Random.Range(0, 2) == 0;
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

    }
}
