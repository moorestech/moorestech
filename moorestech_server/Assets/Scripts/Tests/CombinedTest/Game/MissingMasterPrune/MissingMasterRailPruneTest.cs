using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Interface;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.Train.RailPositions;
using Game.Train.RailGraph;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.MissingMasterPrune
{
    /// <summary>マスタ欠損で除去したレールブロックの巻き添え（上に載る列車・つながるレール接続）の退避と、ロードの通過を見る</summary>
    /// <summary>Covers archiving the collateral of a rail block pruned for a missing master (trains on it, connections to it) and that the load still passes</summary>
    public class MissingMasterRailPruneTest
    {
        private static readonly Vector3Int[] RailPositions = { new(0, 0, 0), new(0, 0, 3), new(0, 0, 6) };
        private static readonly Vector3Int RemovedRailPosition = RailPositions[2];
        private const int CargoCount = 5;

        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        // 除去しないとRailPosition復元がnullになり、列車は貨車と積荷ごと次のautosaveで無音に消える
        // Without pruning, RailPosition restore yields null and the train vanishes silently with its cars and cargo on the next autosave
        [Test]
        public void マスタ欠損のレールに載る列車は積荷ごと退避されロードが通るTest()
        {
            var save = BuildTrainOnRailsSave(out var cargoItemGuid);
            var railBlock = ((JArray)save["world"]).OfType<JObject>().Single(block => (int)block["X"] == RemovedRailPosition.x && (int)block["Z"] == RemovedRailPosition.z);
            railBlock["blockGuid"] = SaveLoadPreparerTestFixture.MissingGuid;

            var (reportStore, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            LogAssert.Expect(LogType.Warning, new Regex("レールに載る列車を、貨車と積荷ごとセーブから除去します"));
            LogAssert.Expect(LogType.Warning, new Regex("つながるレール接続をセーブから除去しました"));
            var prepared = preparer.Prepare(save.ToString());
            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);

            // 列車は通知件数の意味を変えないため件数に入らず、ブロック1件だけが通知に載る
            // Trains stay out of the notice counts so their meaning is unchanged; only the one block reaches the notice
            Assert.AreEqual(1, reportStore.Report.RemovedBlockCount);
            Assert.AreEqual(0, reportStore.Report.EmptiedItemStackCount);

            // 退避ファイルに列車が積荷ごと、切れたレール接続が端の座標ごと残る
            // The archive keeps the train with its cargo and the cut rail connections with their end positions
            var pruned = JObject.Parse(File.ReadAllText(Directory.GetFiles(WorldDataDirectory.FromWorldRoot(_archiveRoot).SavePrunedDirectory, "*.json").Single()));
            Assert.AreEqual(1, ((JArray)pruned["trainUnits"]).Count);
            StringAssert.Contains(cargoItemGuid.ToString(), pruned["trainUnits"][0]["Cars"][0]["ContainerSaveData"].Value<string>());
            var prunedSegments = (JArray)pruned["railSegments"];
            Assert.Greater(prunedSegments.Count, 0);
            Assert.IsTrue(prunedSegments.All(TouchesRemovedRail));

            // ロード対象からは外れ、残ったレール同士の接続は残る
            // They leave the load target while connections between the remaining rails stay
            Assert.AreEqual(0, ((JArray)prepared.Save["trainUnits"]).Count);
            var remainingSegments = (JArray)prepared.Save["railSegments"];
            Assert.Greater(remainingSegments.Count, 0);
            Assert.IsFalse(remainingSegments.Any(TouchesRemovedRail));

            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            var loader = (WorldLoaderFromJson)loadEnvironment.ServiceProvider.GetService<IWorldSaveDataLoader>();
            Assert.DoesNotThrow(() => loader.Load(prepared.Save));
            Assert.AreEqual(0, loadEnvironment.GetITrainLookupDatastore().GetRegisteredTrains().Count());
        }

        // 除去器をすり抜けて復元できない列車が来たら、次のautosaveで消える前に理由が読めること
        // An unrestorable train that slipped past the pruner must leave a readable reason before the next autosave drops it
        [Test]
        public void レール位置を解決できない列車は理由をログに出して読み飛ばされるTest()
        {
            var save = BuildTrainOnRailsSave(out _);
            save["world"] = new JArray();
            save["railSegments"] = new JArray();

            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            var loader = (WorldLoaderFromJson)loadEnvironment.ServiceProvider.GetService<IWorldSaveDataLoader>();
            LogAssert.Expect(LogType.Warning, new Regex("列車のレール位置を解決できないため復元せず読み飛ばします"));
            loader.Load(save);

            Assert.AreEqual(0, loadEnvironment.GetITrainLookupDatastore().GetRegisteredTrains().Count());
        }

        private static bool TouchesRemovedRail(JToken segment)
        {
            return new[] { segment["A"], segment["B"] }.Any(end => (int)end["blockPosition"]["x"] == RemovedRailPosition.x && (int)end["blockPosition"]["z"] == RemovedRailPosition.z);
        }

        // 3本のレールをつなぎ、全ノードにまたがる列車を積荷つきで載せた実DIのセーブ（TrainDiagramSaveLoadTestと同じ組み方）
        // A real DI save with three connected rails and a loaded train spanning every node, built the same way as TrainDiagramSaveLoadTest
        private static JObject BuildTrainOnRailsSave(out Guid cargoItemGuid)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var components = RailPositions.Select(position => TrainTestHelper.PlaceRail(environment, position, BlockDirection.North)).ToArray();

            // 実際の線路と同じく前後双方のノードを明示的な距離で結ぶ
            // Connect both front and back nodes with an explicit distance, as real track does
            const int segmentLength = 2000;
            for (var i = 0; i < components.Length - 1; i++)
            {
                components[i].FrontNode.ConnectNode(components[i + 1].FrontNode, segmentLength);
                components[i + 1].BackNode.ConnectNode(components[i].BackNode, segmentLength);
            }

            var railNodes = components.Reverse().Select(component => (IRailNode)component.FrontNode).ToList();
            var trainLength = segmentLength * 2 / 3;
            var carGuid = MasterHolder.TrainUnitMaster.Train.TrainCars.First().TrainCarGuid;
            var (car, container) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, carGuid, 400000, 1, trainLength, true);
            cargoItemGuid = MasterHolder.ItemMaster.GetItemMaster(new ItemId(1)).ItemGuid;
            container.SetItem(0, new ItemId(1), CargoCount);

            var train = new TrainUnit(new RailPosition(railNodes, trainLength, 0), new List<TrainCar> { car }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            environment.GetITrainUnitMutationDatastore().RegisterTrain(train);

            var save = JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider));
            Assert.AreEqual(1, ((JArray)save["trainUnits"]).Count, "テストの土台に列車が保存されていません");
            Assert.Greater(((JArray)save["railSegments"]).Count, 0, "テストの土台にレール接続が保存されていません");

            train.OnDestroy();
            environment.GetTrainUnitDatastore().Reset();
            environment.GetRailGraphDatastore().Reset();
            return save;
        }
    }
}
