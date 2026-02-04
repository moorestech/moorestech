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
using System.Linq;

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
            var env = TrainTestHelper.CreateEnvironment();
            var worldBlockDatastore = env.WorldBlockDatastore;
            var assembleSaveJsonText = env.ServiceProvider.GetService<AssembleSaveJsonText>();

            var pos = new Vector3Int(10, 0, 10);
            var (_, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainRail,
                pos,
                BlockDirection.North);
            Assert.IsNotNull(railComponents, "RailComponentが取得できませんでした");

            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log("[RailComponentSaveLoadTest] SaveJson:\n" + json);

            worldBlockDatastore.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            env.GetRailGraphDatastore().Reset();

            var loadEnv = TrainTestHelper.CreateEnvironment();
            var loadJson = loadEnv.ServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.IsNotNull(loadJson, "WorldLoaderFromJsonが解決できませんでした");
            loadJson.Load(json);

            var loadedRailBlock = loadEnv.WorldBlockDatastore.GetBlock(pos);
            Assert.IsNotNull(loadedRailBlock, "RailBlockが正しくロードされていません");

            var loadedRailComponents = loadedRailBlock.GetComponents<RailComponent>();
            Assert.IsNotNull(loadedRailComponents, "ロード後のRailComponentがnullです");

            var isDestroy = loadedRailComponents[0].IsDestroy;
            Assert.AreEqual(false, isDestroy, "RailComponentが読み込まれませんでした");
        }

        [Test]
        public void TrainRailMultiBlockSaveLoadTest()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var worldBlockDatastore = env.WorldBlockDatastore;
            var assembleSaveJsonText = env.ServiceProvider.GetService<AssembleSaveJsonText>();

            const int num = 8;
            var allRailComponents = new RailComponent[num];
            var positions = new Vector3Int[num];

            for (int i = 0; i < num; i++)
            {
                positions[i] = new Vector3Int(UnityEngine.Random.Range(-100, 100), 0, UnityEngine.Random.Range(-100, 100));
                var (_, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                    env,
                    ForUnitTestModBlockId.TestTrainRail,
                    positions[i],
                    BlockDirection.North);
                Assert.IsNotNull(railComponents, $"RailComponent[{i}]が取得できませんでした");
                allRailComponents[i] = railComponents[0];
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
                    //allRailComponents[i].ConnectRailComponent(allRailComponents[j], isFrontThis, isFrontTarget);
                    var tmpn0 = isFrontThis ? allRailComponents[i].FrontNode : allRailComponents[i].BackNode;
                    var tmpn2 = isFrontTarget ? allRailComponents[j].FrontNode : allRailComponents[j].BackNode;
                    tmpn0.ConnectNode(tmpn2);
                    tmpn2.OppositeRailNode.ConnectNode(tmpn0.OppositeRailNode);
                    allConnect[(i, j, isFrontThis, isFrontTarget)] = true;
                    allConnect[(j, i, !isFrontTarget, !isFrontThis)] = true;
                }
            }

            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log("[RailComponentSaveLoadTest] SaveJson:\n" + json);

            for (int i = 0; i < num; i++)
            {
                worldBlockDatastore.RemoveBlock(positions[i], BlockRemoveReason.ManualRemove);
            }

            env.GetRailGraphDatastore().Reset();

            var loadEnv = TrainTestHelper.CreateEnvironment();
            var loadJson = loadEnv.ServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.IsNotNull(loadJson, "WorldLoaderFromJsonが解決できませんでした");
            loadJson.Load(json);

            for (int i = 0; i < num; i++)
            {
                var loadedRailBlock = loadEnv.WorldBlockDatastore.GetBlock(positions[i]);
                Assert.IsNotNull(loadedRailBlock, "RailBlockが正しくロードされていません");

                var loadedRailComponents = loadedRailBlock.GetComponents<RailComponent>();
                Assert.IsNotNull(loadedRailComponents, "ロード後のRailComponentがnullです");
                allRailComponents[i] = loadedRailComponents[0];
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
            var env = TrainTestHelper.CreateEnvironment();
            var blockStore = env.WorldBlockDatastore;
            var saveJsonAssembler = env.ServiceProvider.GetService<AssembleSaveJsonText>();

            var stationPos = new Vector3Int(0, 0, 0);
            var (stationBlock, stationComponent) = TrainTestHelper.PlaceBlockWithComponent<StationComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                stationPos,
                BlockDirection.North);
            Assert.IsNotNull(stationBlock, "駅ブロックの設置に失敗しました");
            Assert.IsNotNull(stationComponent, "StationComponentが見つかりません");

            var inputChestPos = new Vector3Int(-1, 0, 2);
            var (_, inputInventory) = TrainTestHelper.PlaceBlockWithComponent<IBlockInventory>(
                env,
                ForUnitTestModBlockId.ChestId,
                inputChestPos,
                BlockDirection.North);

            var testItemStack = ServerContext.ItemStackFactory.Create(new ItemId(1), 10);
            inputInventory.InsertItem(testItemStack, InsertItemContext.Empty);

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

            blockStore.RemoveBlock(stationPos, BlockRemoveReason.ManualRemove);
            blockStore.RemoveBlock(inputChestPos, BlockRemoveReason.ManualRemove);
            env.GetRailGraphDatastore().Reset();

            var loadEnv = TrainTestHelper.CreateEnvironment();
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

            var outputChestPos = new Vector3Int(-1, 0, 5);
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

        [Test]
        public void TrainStationAnimationProgressPersistsAcrossSaveLoad()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var (stationBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                environment,
                ForUnitTestModBlockId.TestTrainStation,
                Vector3Int.zero,
                BlockDirection.North);
            Assert.IsNotNull(stationBlock, "駅ブロックの生成に失敗しました。");
            Assert.IsNotNull(railComponents, "駅ブロックのRailComponent取得に失敗しました。");

            var stationComponent = stationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(stationComponent, "StationComponentの取得に失敗しました。");
            Assert.IsTrue(stationBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var stationInventory), "駅インベントリの取得に失敗しました。");

            var stationParam = (TrainStationBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockParam;
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            stationInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var segmentLength = stationBlock.BlockPositionInfo.BlockSize.z;
            var railPosition = new RailPosition(new List<IRailNode> { exitNode, entryNode }, segmentLength, 0);
            var trainCar = TrainTestCarFactory.CreateTrainCar(0, 1000, 1, segmentLength, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            trainUnit.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(trainCar.IsDocked, "列車が駅にドッキングしていません。");

            var elapsedTicks = stationParam.LoadingSpeed / 2;
            for (var i = 0; i < elapsedTicks; i++) stationComponent.Update();
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "セーブ前に一括移送が発生しています。");

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);
            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            var loadedBlock = loadEnvironment.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsNotNull(loadedBlock, "ロード後に駅ブロックが見つかりません。");
            var loadedStation = loadedBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(loadedStation, "ロード後にStationComponentが見つかりません。");
            Assert.IsTrue(loadedBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var loadedInventory), "ロード後の駅インベントリ取得に失敗しました。");

            var loadedTrain = loadEnvironment.GetTrainUpdateService().GetRegisteredTrains().Single();
            var loadedCar = loadedTrain.Cars[0];
            Assert.IsTrue(loadedCar.IsDocked, "ロード後に列車ドッキング状態が復元されていません。");

            var remainingTicks = stationParam.LoadingSpeed + 1 - elapsedTicks;
            for (var i = 0; i < remainingTicks; i++) loadedStation.Update();

            var platformStack = loadedInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, platformStack.Id, "ロード後に駅アニメーション進捗が復元されていません。");
            var carStack = loadedCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Id, "ロード後に列車側へアイテムが移送されていません。");
            Assert.AreEqual(maxStack, carStack.Count, "ロード後に列車側へ全量移送されていません。");

            loadedTrain.trainUnitStationDocking.UndockFromStation();
            loadEnvironment.GetTrainDiagramManager().UnregisterDiagram(loadedTrain.trainDiagram);
            loadEnvironment.GetTrainUpdateService().UnregisterTrain(loadedTrain);
        }

        private static void AssertConnection(IEnumerable<IRailNode> nodes, IRailNode expected)
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
