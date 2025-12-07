using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Blocks.TrainRail;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainDiagramSaveLoadTest
    {
        private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };

        [Test]
        public void DiagramEntriesAreRestoredFromSaveData()
        {
            RailGraphDatastore.ResetInstance();
            TrainUpdateService.Instance.ResetTrains();

            var context = CreateTrainDiagramContext();

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(context.Environment.ServiceProvider);

            CleanupOriginalContext(context);

            var loadEnv = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnv.ServiceProvider, saveJson);

            var loadedTrains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            Assert.AreEqual(1, loadedTrains.Count, "ロード後の列車数が一致しません。");

            var loadedTrain = loadedTrains[0];
            Assert.AreEqual(context.ExpectedEntries.Count, loadedTrain.trainDiagram.Entries.Count,
                "ロード後のダイアグラムエントリ数が一致しません。");
            Assert.AreEqual(context.ExpectedCurrentIndex, loadedTrain.trainDiagram.CurrentIndex,
                "ロード後のダイアグラム現在インデックスが一致しません。");

            for (var i = 0; i < context.ExpectedEntries.Count; i++)
            {
                var expected = context.ExpectedEntries[i];
                var actual = loadedTrain.trainDiagram.Entries[i];

                Assert.AreEqual(expected.EntryId, actual.entryId, $"エントリ{i}のIDが一致しません。");
                Assert.IsTrue(RailGraphDatastore.TryGetConnectionDestination(actual.Node, out var connection),
                    $"エントリ{i}のRailComponentIDを解決できません。");

                var destination = connection.railComponentID;
                var actualPosition = new Vector3Int(destination.Position.x, destination.Position.y, destination.Position.z);

                Assert.AreEqual(expected.Position, actualPosition, $"エントリ{i}の座標が一致しません。");
                Assert.AreEqual(expected.ComponentIndex, destination.ID, $"エントリ{i}のコンポーネントIDが一致しません。");
                Assert.AreEqual(expected.IsFront, connection.IsFront, $"エントリ{i}の接続面情報が一致しません。");
                Assert.AreEqual(expected.WaitInitial, actual.GetWaitForTicksInitialTicks(),
                    $"エントリ{i}の待機初期値が一致しません。");
                Assert.AreEqual(expected.WaitRemaining, actual.GetWaitForTicksRemainingTicks(),
                    $"エントリ{i}の待機残数が一致しません。");
            }

            CleanupLoadedState(loadEnv, loadedTrains, context.RailPositions);
        }

        [Test]
        public void DiagramEntriesWithMissingRailsAreSkippedDuringLoad()
        {
            RailGraphDatastore.ResetInstance();
            TrainUpdateService.Instance.ResetTrains();

            var context = CreateTrainDiagramContext();

            var originalJson = SaveLoadJsonTestHelper.AssembleSaveJson(context.Environment.ServiceProvider);
            var corruptedJson = CorruptDiagramEntry(originalJson, context.ExpectedEntries[^1].EntryId);

            CleanupOriginalContext(context);

            var loadEnv = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnv.ServiceProvider, corruptedJson);

            var loadedTrains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            Assert.AreEqual(1, loadedTrains.Count, "破損データロード後の列車数が一致しません。");

            var loadedTrain = loadedTrains[0];
            Assert.AreEqual(context.ExpectedEntries.Count - 1, loadedTrain.trainDiagram.Entries.Count,
                "破損データロード後のダイアグラムエントリ数が期待と一致しません。");

            var expectedIndex = Math.Min(context.ExpectedCurrentIndex, loadedTrain.trainDiagram.Entries.Count - 1);
            Assert.AreEqual(expectedIndex, loadedTrain.trainDiagram.CurrentIndex,
                "破損データロード後のダイアグラム現在インデックスが調整されていません。");

            for (var i = 0; i < loadedTrain.trainDiagram.Entries.Count; i++)
            {
                var expected = context.ExpectedEntries[i];
                var actual = loadedTrain.trainDiagram.Entries[i];

                Assert.AreEqual(expected.EntryId, actual.entryId, $"エントリ{i}のIDが一致しません。");
                Assert.IsTrue(RailGraphDatastore.TryGetConnectionDestination(actual.Node, out var connection),
                    $"エントリ{i}のRailComponentIDを解決できません。");

                var destination = connection.railComponentID;
                var actualPosition = new Vector3Int(destination.Position.x, destination.Position.y, destination.Position.z);

                Assert.AreEqual(expected.Position, actualPosition, $"エントリ{i}の座標が一致しません。");
                Assert.AreEqual(expected.ComponentIndex, destination.ID, $"エントリ{i}のコンポーネントIDが一致しません。");
                Assert.AreEqual(expected.IsFront, connection.IsFront, $"エントリ{i}の接続面情報が一致しません。");
                Assert.AreEqual(expected.WaitInitial, actual.GetWaitForTicksInitialTicks(),
                    $"エントリ{i}の待機初期値が一致しません。");
                Assert.AreEqual(expected.WaitRemaining, actual.GetWaitForTicksRemainingTicks(),
                    $"エントリ{i}の待機残数が一致しません。");
            }

            CleanupLoadedState(loadEnv, loadedTrains, context.RailPositions);
        }

        private static TrainDiagramTestContext CreateTrainDiagramContext()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railPositions = new[]
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(0, 0, 3),
                new Vector3Int(0, 0, 6)
            };

            var components = new RailComponent[railPositions.Length];
            for (var i = 0; i < railPositions.Length; i++)
            {
                components[i] = TrainTestHelper.PlaceRail(environment, railPositions[i], BlockDirection.North);
            }

            const int SegmentLength = 2000;
            for (var i = 0; i < components.Length - 1; i++)
            {
                var current = components[i];
                var next = components[i + 1];

                // 実際の線路では双方向の接続が保存されるため、前後双方のノードを明示的な距離で結ぶ。
                current.ConnectRailComponent(next, true, true, SegmentLength);
                next.ConnectRailComponent(current, true, true, SegmentLength);
            }

            var railNodes = new List<RailNode>();
            for (var i = components.Length - 1; i >= 0; i--)
            {
                railNodes.Add(components[i].FrontNode);
            }

            var totalDistance = 0;
            for (var i = 0; i < railNodes.Count - 1; i++)
            {
                var distance = railNodes[i + 1].GetDistanceToNode(railNodes[i]);
                Assert.Greater(distance, 0, $"RailNode間の距離が不正です (index {i}).");
                totalDistance += distance;
            }

            var trainLength = Math.Max(1, totalDistance / 3);
            var railPosition = new RailPosition(new List<RailNode>(railNodes), trainLength, 0);
            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();
            var cars = new List<TrainCar>
            {
                new TrainCar(new TrainCarMasterElement(firstTrain.TrainCarGuid, firstTrain.ItemGuid, null, 1000, 1, trainLength))
            };
            var train = new TrainUnit(railPosition, cars);

            foreach (var component in components)
            {
                train.trainDiagram.AddEntry(component.FrontNode);
            }

            train.trainDiagram.Entries[1].SetDepartureWaitTicks(5);
            train.trainDiagram.MoveToNextEntry();
            train.trainDiagram.MoveToNextEntry();

            var expectedEntries = new List<ExpectedEntry>(train.trainDiagram.Entries.Count);
            for (var i = 0; i < train.trainDiagram.Entries.Count; i++)
            {
                var entry = train.trainDiagram.Entries[i];
                Assert.IsTrue(RailGraphDatastore.TryGetConnectionDestination(entry.Node, out var connection),
                    $"エントリ{i}のRailComponentIDを取得できません。");

                var destination = connection.railComponentID;
                var position = new Vector3Int(destination.Position.x, destination.Position.y, destination.Position.z);

                expectedEntries.Add(new ExpectedEntry(
                    entry.entryId,
                    position,
                    destination.ID,
                    connection.IsFront,
                    entry.GetWaitForTicksInitialTicks(),
                    entry.GetWaitForTicksRemainingTicks()));
            }

            return new TrainDiagramTestContext(environment, train, expectedEntries, train.trainDiagram.CurrentIndex, railPositions);
        }

        private static void CleanupOriginalContext(TrainDiagramTestContext context)
        {
            context.Train.OnDestroy();
            foreach (var position in context.RailPositions)
            {
                context.Environment.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            }

            TrainUpdateService.Instance.ResetTrains();
            RailGraphDatastore.ResetInstance();
        }

        private static void CleanupLoadedState(TrainTestEnvironment environment, List<TrainUnit> trains, IReadOnlyList<Vector3Int> railPositions)
        {
            CleanupTrains(trains);
            foreach (var position in railPositions)
            {
                environment.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            }

            TrainUpdateService.Instance.ResetTrains();
            RailGraphDatastore.ResetInstance();
        }

        private static void CleanupTrains(IEnumerable<TrainUnit> trains)
        {
            foreach (var train in trains)
            {
                train?.OnDestroy();
            }
        }

        private static string CorruptDiagramEntry(string originalJson, Guid targetEntryId)
        {
            var root = JsonNode.Parse(originalJson) as JsonObject;
            Assert.IsNotNull(root, "セーブデータJSONの解析に失敗しました。");

            var trainUnits = root["trainUnits"] as JsonArray;
            Assert.IsNotNull(trainUnits, "セーブデータにtrainUnits配列が存在しません。");
            Assert.IsTrue(trainUnits.Count > 0, "セーブデータに列車情報が含まれていません。");

            var trainObject = trainUnits[0] as JsonObject;
            Assert.IsNotNull(trainObject, "trainUnits[0]がJSONオブジェクトではありません。");

            var diagramObject = trainObject["Diagram"] as JsonObject;
            Assert.IsNotNull(diagramObject, "trainUnits[0].DiagramがJSONオブジェクトではありません。");

            var entries = diagramObject["Entries"] as JsonArray;
            Assert.IsNotNull(entries, "Diagram.Entriesが配列ではありません。");

            var found = false;
            foreach (var entryNode in entries)
            {
                if (entryNode is not JsonObject entryObject)
                {
                    continue;
                }

                if (!entryObject.TryGetPropertyValue("EntryId", out var idNode) || idNode is null)
                {
                    continue;
                }

                if (!Guid.TryParse(idNode.ToString(), out var entryId) || entryId != targetEntryId)
                {
                    continue;
                }

                var nodeObject = entryObject["Node"] as JsonObject;
                Assert.IsNotNull(nodeObject, "ダイアグラムエントリにNode情報が存在しません。");

                var destinationObject = nodeObject["railComponentID"] as JsonObject;
                Assert.IsNotNull(destinationObject, "ダイアグラムエントリにrailComponentIDが存在しません。");

                var positionObject = destinationObject["Position"] as JsonObject ?? new JsonObject();
                positionObject["x"] = 99;
                positionObject["y"] = 0;
                positionObject["z"] = 0;
                destinationObject["Position"] = positionObject;

                found = true;
                break;
            }

            Assert.IsTrue(found, "破損させる対象のダイアグラムエントリが見つかりません。");

            return root.ToJsonString(CompactJsonOptions);
        }

        private readonly struct ExpectedEntry
        {
            public ExpectedEntry(Guid entryId, Vector3Int position, int componentIndex, bool isFront, int? waitInitial, int? waitRemaining)
            {
                EntryId = entryId;
                Position = position;
                ComponentIndex = componentIndex;
                IsFront = isFront;
                WaitInitial = waitInitial;
                WaitRemaining = waitRemaining;
            }

            public Guid EntryId { get; }
            public Vector3Int Position { get; }
            public int ComponentIndex { get; }
            public bool IsFront { get; }
            public int? WaitInitial { get; }
            public int? WaitRemaining { get; }
        }

        private sealed class TrainDiagramTestContext
        {
            public TrainDiagramTestContext(TrainTestEnvironment environment, TrainUnit train, List<ExpectedEntry> expectedEntries,
                int expectedCurrentIndex, IReadOnlyList<Vector3Int> railPositions)
            {
                Environment = environment;
                Train = train;
                ExpectedEntries = expectedEntries;
                ExpectedCurrentIndex = expectedCurrentIndex;
                RailPositions = railPositions;
            }

            public TrainTestEnvironment Environment { get; }
            public TrainUnit Train { get; }
            public List<ExpectedEntry> ExpectedEntries { get; }
            public int ExpectedCurrentIndex { get; }
            public IReadOnlyList<Vector3Int> RailPositions { get; }
        }
    }
}
