using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.Train.RailGraph;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Core.Master;
using Core.Update;
using System.Collections.Generic;

namespace Tests.UnitTest.Game.SaveLoad
{
    /// <summary>
    ///     RailComponent関連のセーブ・ロードを検証するテスト
    /// </summary>
    public class TrainRailSaveLoadTest
    {
        [Test]
        public void TrainRailOneBlockSaveLoadTest()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            var worldBlockDatastore = env.WorldBlockDatastore;
            var assembleSaveJsonText = env.ServiceProvider.GetService<AssembleSaveJsonText>();

            var pos = new Vector3Int(10, 0, 10);
            var (_, railSaverComponent) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainRail,
                pos,
                BlockDirection.North);
            Assert.IsNotNull(railSaverComponent, "RailSaverComponentが取得できませんでした");

            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log("[RailComponentSaveLoadTest] SaveJson:\n" + json);

            worldBlockDatastore.RemoveBlock(pos);
            RailGraphDatastore.ResetInstance();

            var loadEnv = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            var loadJson = loadEnv.ServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.IsNotNull(loadJson, "WorldLoaderFromJsonが解決できませんでした");
            loadJson.Load(json);

            var loadedRailBlock = loadEnv.WorldBlockDatastore.GetBlock(pos);
            Assert.IsNotNull(loadedRailBlock, "RailBlockが正しくロードされていません");

            var loadedRailComp = loadedRailBlock.GetComponent<RailSaverComponent>();
            Assert.IsNotNull(loadedRailComp, "ロード後のRailComponentがnullです");

            var isDestroy = loadedRailComp.RailComponents[0].IsDestroy;
            Assert.AreEqual(false, isDestroy, "RailComponentが読み込まれませんでした");
        }

        [Test]
        public void TrainRailMultiBlockSaveLoadTest()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            var worldBlockDatastore = env.WorldBlockDatastore;
            var assembleSaveJsonText = env.ServiceProvider.GetService<AssembleSaveJsonText>();

            const int num = 8;
            var allRailComponents = new RailComponent[num];
            var positions = new Vector3Int[num];

            for (int i = 0; i < num; i++)
            {
                positions[i] = new Vector3Int(UnityEngine.Random.Range(-100, 100), 0, UnityEngine.Random.Range(-100, 100));
                var (_, railSaverComponent) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                    env,
                    ForUnitTestModBlockId.TestTrainRail,
                    positions[i],
                    BlockDirection.North);
                Assert.IsNotNull(railSaverComponent, $"RailSaverComponent[{i}]が取得できませんでした");
                allRailComponents[i] = railSaverComponent.RailComponents[0];
            }

            var allConnect = new Dictionary<(int, int, bool, bool), bool>();
            for (int i = 0; i < num; i++)
            {
                for (int j = 0; j < num; j++)
                {
                    if (i == j) continue;
                    if (UnityEngine.Random.Range(0, 5) != 0) continue;

                    bool isFrontThis = UnityEngine.Random.Range(0, 2) == 0;
                    bool isFrontTarget = UnityEngine.Random.Range(0, 2) == 0;

                    allRailComponents[i].ConnectRailComponent(allRailComponents[j], isFrontThis, isFrontTarget);
                    allConnect[(i, j, isFrontThis, isFrontTarget)] = true;
                    allConnect[(j, i, !isFrontTarget, !isFrontThis)] = true;
                }
            }

            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log("[RailComponentSaveLoadTest] SaveJson:\n" + json);

            for (int i = 0; i < num; i++)
            {
                worldBlockDatastore.RemoveBlock(positions[i]);
            }

            RailGraphDatastore.ResetInstance();

            var loadEnv = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            var loadJson = loadEnv.ServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.IsNotNull(loadJson, "WorldLoaderFromJsonが解決できませんでした");
            loadJson.Load(json);

            for (int i = 0; i < num; i++)
            {
                var loadedRailBlock = loadEnv.WorldBlockDatastore.GetBlock(positions[i]);
                Assert.IsNotNull(loadedRailBlock, "RailBlockが正しくロードされていません");

                var loadedRailComp = loadedRailBlock.GetComponent<RailSaverComponent>();
                Assert.IsNotNull(loadedRailComp, "ロード後のRailComponentがnullです");
                allRailComponents[i] = loadedRailComp.RailComponents[0];
            }

            for (int i = 0; i < num; i++)
            {
                for (int j = 0; j < num; j++)
                {
                    if (i == j) continue;

                    if (allConnect.ContainsKey((i, j, true, true)))
                    {
                        AssertConnection(allRailComponents[i].FrontNode.ConnectedNodes, allRailComponents[j].FrontNode);
                    }

                    if (allConnect.ContainsKey((i, j, true, false)))
                    {
                        AssertConnection(allRailComponents[i].FrontNode.ConnectedNodes, allRailComponents[j].BackNode);
                    }

                    if (allConnect.ContainsKey((i, j, false, true)))
                    {
                        AssertConnection(allRailComponents[i].BackNode.ConnectedNodes, allRailComponents[j].FrontNode);
                    }

                    if (allConnect.ContainsKey((i, j, false, false)))
                    {
                        AssertConnection(allRailComponents[i].BackNode.ConnectedNodes, allRailComponents[j].BackNode);
                    }
                }
            }
        }

        [Test]
        public void TrainStationSaveLoadTest()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            var blockStore = env.WorldBlockDatastore;
            var saveJsonAssembler = env.ServiceProvider.GetService<AssembleSaveJsonText>();

            var stationPos = new Vector3Int(0, 0, 0);
            var (stationBlock, stationComponent) = TrainTestHelper.PlaceBlockWithComponent<StationComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                stationPos,
                BlockDirection.North);
            Assert.IsNotNull(stationBlock, "駐車場ブロックの設置に失敗しました");
            Assert.IsNotNull(stationComponent, "StationComponentが見つかりません");

            var inputChestPos = new Vector3Int(4, 0, -1);
            var (_, inputInventory) = TrainTestHelper.PlaceBlockWithComponent<IBlockInventory>(
                env,
                ForUnitTestModBlockId.ChestId,
                inputChestPos,
                BlockDirection.North);

            var testItemStack = ServerContext.ItemStackFactory.Create(new ItemId(1), 10);
            inputInventory.InsertItem(testItemStack);

            var insertedStack = inputInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), insertedStack.Id, "チェストへの挿入に失敗しました");
            Assert.AreEqual(10, insertedStack.Count, "チェスト内のアイテム数が一致しません");

            var stationInventory = stationBlock.GetComponent<IBlockInventory>();
            Assert.IsNotNull(stationInventory, "Stationのインベントリが取得できません");

            for (int i = 0; i < 10; i++)
            {
                GameUpdater.Update();
            }

            var inputChestRemainder = inputInventory.GetItem(0);
            Assert.IsTrue(inputChestRemainder.Count < 10, "入力チェスト内のアイテム数が減っていません");

            var saveJson = saveJsonAssembler.AssembleSaveJson();
            Debug.Log("[TrainStationSaveLoadTest] SaveJson:\n" + saveJson);

            blockStore.RemoveBlock(stationPos);
            blockStore.RemoveBlock(inputChestPos);
            RailGraphDatastore.ResetInstance();

            var loadEnv = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);
            var worldLoader = loadEnv.ServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.IsNotNull(worldLoader, "WorldLoaderFromJsonが解決できませんでした");
            worldLoader.Load(saveJson);

            blockStore = loadEnv.WorldBlockDatastore;
            var loadedStationBlock = blockStore.GetBlock(stationPos);
            Assert.IsNotNull(loadedStationBlock, "ロード後にStationブロックが見つかりません");

            var loadedStationComponent = loadedStationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(loadedStationComponent, "ロード後のStationComponentが見つかりません");

            var loadedStationInventory = loadedStationBlock.GetComponent<IBlockInventory>();
            var stationStack = loadedStationInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), stationStack.Id, "ロード後のStationインベントリにアイテムが存在しません");
            Assert.IsTrue(stationStack.Count > 0, "ロード後のStationインベントリ数が0です");

            var outputChestPos = new Vector3Int(6, 0, -1);
            var (_, outputInventory) = TrainTestHelper.PlaceBlockWithComponent<IBlockInventory>(
                loadEnv,
                ForUnitTestModBlockId.ChestId,
                outputChestPos,
                BlockDirection.North);

            for (int i = 0; i < 10; i++)
            {
                GameUpdater.Update();
            }

            var outputStack = outputInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), outputStack.Id, "出力チェストにアイテムが移動していません");
            Assert.IsTrue(outputStack.Count > 0, "出力チェストのアイテム数が0です");
        }

        private static void AssertConnection(IEnumerable<RailNode> nodes, RailNode expected)
        {
            var isConnect = false;
            foreach (var node in nodes)
            {
                if (node == expected)
                {
                    isConnect = true;
                    break;
                }
            }

            Assert.IsTrue(isConnect, "接続情報が正しくロードされていません");
        }
    }
}
