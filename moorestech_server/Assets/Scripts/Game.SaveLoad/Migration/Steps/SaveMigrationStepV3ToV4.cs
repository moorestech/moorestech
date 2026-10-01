using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    public sealed class SaveMigrationStepV3ToV4 : ISaveMigrationStep
    {
        private const string BeltSaveKey = "Game.Block.Blocks.BeltConveyor.VanillaBeltConveyorComponent";
        public int FromVersion => 3;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (save["world"] is not JArray world) return Fail("world must be an array.");
            var conversions = new List<(JObject State, JArray Items)>();
            foreach (var token in world)
            {
                if (token is not JObject block || block["state"] is not JObject state) return Fail("Block state must be an object.");
                var value = state[BeltSaveKey];
                if (value == null) continue;
                // 適用済みの包みは再実行でもそのまま保つ。
                // Preserve an already migrated wrapper on repeated application.
                if (value is JObject wrapped && wrapped["legacyItems"] is JArray) continue;
                if (value is not JArray items) return Fail($"Belt state on block {block["instanceId"]} must be an array.");
                foreach (var item in items)
                {
                    if (item.Type == JTokenType.Null) continue;
                    if (!TryReadLegacyItem(item, out var parsed, out var reason)) return Fail(reason);
                    if (parsed["itemStack"]?.Type == JTokenType.Null) continue;
                    if (parsed["itemStack"] is not JObject || parsed["remainingSeconds"]?.Type is not (JTokenType.Float or JTokenType.Integer))
                        return Fail("A legacy belt item lacks its itemStack or remainingSeconds.");
                }
                conversions.Add((state, items));
            }
            // 秒数とコネクターはロード時に実マスタで位置へ変換する。
            // Runtime loading maps seconds and connectors with the actual master.
            foreach (var conversion in conversions)
                conversion.State[BeltSaveKey] = new JObject { ["legacyItems"] = conversion.Items.DeepClone() };
            Debug.Log($"Migrated belt state V3 to V4: wrapped {conversions.Count} blocks.");
            return SaveMigrationStepResult.Converted(save);
        }

        private static bool TryReadLegacyItem(JToken token, out JObject item, out string reason)
        {
            item = token as JObject;
            reason = null;
            if (item != null) return true;
            if (token.Type != JTokenType.String) { reason = "Legacy belt item must contain JSON object text."; return false; }
            // 外部保存データのJSON解析だけを隔離し、失敗は結果に残す。
            // Isolate external save JSON parsing and return an explicit failure.
            try { item = JObject.Parse((string)token); return true; }
            catch (JsonException exception)
            {
                reason = $"Legacy belt item JSON could not be parsed: {exception.Message}";
                Debug.LogWarning(reason);
                return false;
            }
        }

        private static SaveMigrationStepResult Fail(string reason)
        {
            Debug.LogWarning($"Cannot migrate belt state V3 to V4: {reason}");
            return SaveMigrationStepResult.Failed(reason);
        }
    }
}
