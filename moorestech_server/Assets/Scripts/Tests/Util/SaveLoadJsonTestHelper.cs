using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.Train.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Tests.Util
{
    public static class SaveLoadJsonTestHelper
    {
        private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };

        public static string AssembleSaveJson(ServiceProvider serviceProvider)
        {
            Assert.IsNotNull(serviceProvider, "ServiceProvider が null です。");
            var assembler = serviceProvider.GetRequiredService<AssembleSaveJsonText>();
            return assembler.AssembleSaveJson();
        }

        public static void LoadFromJson(ServiceProvider serviceProvider, string json)
        {
            Assert.IsNotNull(serviceProvider, "ServiceProvider が null です。");
            var loader = serviceProvider.GetRequiredService<IWorldSaveDataLoader>();
            Assert.IsTrue(loader is WorldLoaderFromJson, "WorldLoaderFromJson を解決できませんでした。");
            ((WorldLoaderFromJson)loader).Load(json);
        }

        public static void RemoveTrainUnitAt(JsonNode root, int index)
        {
            Assert.IsNotNull(root, "JSON ルートが null です。");

            var trainUnits = GetRequiredArray(root, "trainUnits");
            Assert.IsTrue(index >= 0 && index < trainUnits.Count,
                $"trainUnits 配列のインデックス {index} が範囲外です (Count: {trainUnits.Count})。");

            trainUnits.RemoveAt(index);
        }

        public static void RemoveTrainUnitDockedAt(JsonNode root, int x, int y, int z)
        {
            Assert.IsNotNull(root, "JSON ルートが null です。");

            var trainUnits = GetRequiredArray(root, "trainUnits");

            for (var i = 0; i < trainUnits.Count; i++)
            {
                if (trainUnits[i] is JsonObject trainObject && TrainUnitHasDockingAt(trainObject, x, y, z))
                {
                    trainUnits.RemoveAt(i);
                    return;
                }
            }

            Assert.Fail($"DockingBlockPosition が (x:{x}, y:{y}, z:{z}) の TrainUnit が見つかりませんでした。");
        }

        public static SaveJsonMutation CreateMutation(ServiceProvider serviceProvider)
        {
            Assert.IsNotNull(serviceProvider, "ServiceProvider が null です。");

            var originalJson = AssembleSaveJson(serviceProvider);
            var root = JsonNode.Parse(originalJson);

            if (root is null)
            {
                throw new InvalidOperationException("セーブJSONの解析に失敗しました。");
            }

            return new SaveJsonMutation(originalJson, root);
        }

        private static JsonArray GetRequiredArray(JsonNode root, string propertyName)
        {
            var rootObject = GetRequiredObject(root, "セーブデータのルートが JSON オブジェクトではありません。");

            if (!rootObject.TryGetPropertyValue(propertyName, out var node))
            {
                Assert.Fail($"セーブデータに '{propertyName}' 配列が存在しません。");
            }

            if (node is JsonArray array)
            {
                return array;
            }

            Assert.Fail($"'{propertyName}' は配列ではありません。");
            return default!;
        }

        private static JsonObject GetRequiredObject(JsonNode node, string failureMessage)
        {
            if (node is JsonObject jsonObject)
            {
                return jsonObject;
            }

            Assert.Fail(failureMessage);
            return default!;
        }

        private static bool TrainUnitHasDockingAt(JsonObject trainObject, int x, int y, int z)
        {
            if (!trainObject.TryGetPropertyValue("Cars", out var carsNode) || carsNode is not JsonArray carsArray)
            {
                return false;
            }

            foreach (var carNode in carsArray)
            {
                if (carNode is not JsonObject carObject)
                {
                    continue;
                }

                if (!carObject.TryGetPropertyValue("DockingBlockPosition", out var dockingNode) || dockingNode is null)
                {
                    continue;
                }

                if (dockingNode is not JsonObject dockingObject)
                {
                    continue;
                }

                if (TryGetInt(dockingObject, "x", out var dx) &&
                    TryGetInt(dockingObject, "y", out var dy) &&
                    TryGetInt(dockingObject, "z", out var dz) &&
                    dx == x && dy == y && dz == z)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetInt(JsonObject jsonObject, string propertyName, out int value)
        {
            value = default;

            if (!jsonObject.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue valueNode)
            {
                return false;
            }

            return valueNode.TryGetValue(out value);
        }

        public readonly struct SaveLoadCycleResult
        {
            public SaveLoadCycleResult(string originalJson, string corruptedJson, JsonNode? corruptedRoot)
            {
                OriginalJson = originalJson;
                CorruptedJson = corruptedJson;
                CorruptedRoot = corruptedRoot;
            }

            public string OriginalJson { get; }
            public string CorruptedJson { get; }
            public JsonNode? CorruptedRoot { get; }
        }

        public sealed class SaveJsonMutation
        {
            internal SaveJsonMutation(string originalJson, JsonNode root)
            {
                OriginalJson = originalJson;
                Root = root;
            }

            public string OriginalJson { get; }

            public JsonNode Root { get; }

            public void RemoveTrainUnitAt(int index)
            {
                SaveLoadJsonTestHelper.RemoveTrainUnitAt(Root, index);
            }

            public void RemoveTrainUnitDockedAt(int x, int y, int z)
            {
                SaveLoadJsonTestHelper.RemoveTrainUnitDockedAt(Root, x, y, z);
            }

            public void RemoveDockingBlockPosition(int trainIndex, int carIndex)
            {
                var trainUnits = GetRequiredArray(Root, "trainUnits");

                Assert.IsTrue(trainIndex >= 0 && trainIndex < trainUnits.Count,
                    $"trainUnits 配列のインデックス {trainIndex} が範囲外です (Count: {trainUnits.Count})。");

                var trainObject = GetRequiredObject(trainUnits[trainIndex],
                    $"trainUnits[{trainIndex}] が JSON オブジェクトではありません。");

                var cars = GetRequiredArray(trainObject, "Cars");

                Assert.IsTrue(carIndex >= 0 && carIndex < cars.Count,
                    $"Cars 配列のインデックス {carIndex} が範囲外です (Count: {cars.Count})。");

                var carObject = GetRequiredObject(cars[carIndex],
                    $"Cars[{carIndex}] が JSON オブジェクトではありません。");

                carObject["DockingBlockPosition"] = JsonNode.Parse("null")!;
            }

            public SaveLoadCycleResult Load(ServiceProvider serviceProvider)
            {
                var corruptedJson = Root.ToJsonString(CompactJsonOptions);
                LoadFromJson(serviceProvider, corruptedJson);
                return new SaveLoadCycleResult(OriginalJson, corruptedJson, Root);
            }
        }

    }
}
