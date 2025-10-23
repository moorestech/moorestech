using System;
using System.Collections.Generic;
using System.Linq;
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

        public static string CorruptJson(string json, Func<string, string> corruptor)
        {
            Assert.IsNotNull(corruptor, "JSON 破壊関数が null です。");
            return corruptor(json);
        }

        public static string CorruptJson(string json, Random random, Func<Random, string, string> corruptor)
        {
            Assert.IsNotNull(random, "Random が null です。");
            Assert.IsNotNull(corruptor, "JSON 破壊関数が null です。");
            return corruptor(random, json);
        }

        public static void RemoveTrainUnitAt(JsonNode root, int index)
        {
            Assert.IsNotNull(root, "JSON ルートが null です。");

            var trainUnits = GetRequiredArray(root, "trainUnits");
            Assert.IsTrue(index >= 0 && index < trainUnits.Count,
                $"trainUnits 配列のインデックス {index} が範囲外です (Count: {trainUnits.Count})。");

            trainUnits.RemoveAt(index);
        }

        public static void RemoveTrainUnit(JsonNode root, Func<JsonObject, bool> predicate)
        {
            Assert.IsNotNull(root, "JSON ルートが null です。");
            Assert.IsNotNull(predicate, "削除条件の述語が null です。");

            var removed = TryRemoveTrainUnit(root, predicate);

            Assert.IsTrue(removed, "指定条件に一致する TrainUnit が見つかりませんでした。");
        }

        public static void RemoveTrainUnitDockedAt(JsonNode root, int x, int y, int z)
        {
            Assert.IsNotNull(root, "JSON ルートが null です。");

            var removed = TryRemoveTrainUnit(root, train => TrainUnitHasDockingAt(train, x, y, z));

            Assert.IsTrue(removed,
                $"DockingBlockPosition が (x:{x}, y:{y}, z:{z}) の TrainUnit が見つかりませんでした。");
        }

        public static SaveLoadCycleResult SaveCorruptAndLoad(
            ServiceProvider serviceProvider,
            Func<string, string> corruptor)
        {
            return SaveCorruptAndLoad(serviceProvider, serviceProvider, corruptor);
        }

        public static SaveLoadCycleResult SaveCorruptAndLoad(
            ServiceProvider saveProvider,
            ServiceProvider loadProvider,
            Func<string, string> corruptor)
        {
            var originalJson = AssembleSaveJson(saveProvider);
            var corruptedJson = CorruptJson(originalJson, corruptor);
            LoadFromJson(loadProvider, corruptedJson);
            return new SaveLoadCycleResult(originalJson, corruptedJson, null);
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

        private static bool TryRemoveTrainUnit(JsonNode root, Func<JsonObject, bool> predicate)
        {
            var trainUnits = GetRequiredArray(root, "trainUnits");

            for (var i = 0; i < trainUnits.Count; i++)
            {
                if (trainUnits[i] is JsonObject trainObject && predicate(trainObject))
                {
                    trainUnits.RemoveAt(i);
                    return true;
                }
            }

            return false;
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

    }
}
