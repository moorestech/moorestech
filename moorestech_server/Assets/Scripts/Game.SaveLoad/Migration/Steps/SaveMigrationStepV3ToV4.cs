using System;
using System.Collections.Generic;
using System.Globalization;
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
                // 包み済み配列も同じ検証を通し、再包装はしない。
                // Validate wrapped arrays through the same boundary without wrapping them again.
                var items = value as JArray;
                bool needsWrapping = items != null;
                if (!needsWrapping && value is JObject wrapped) items = wrapped["legacyItems"] as JArray;
                if (items == null) return Fail($"Belt state on block {block["instanceId"]} must be an array or legacyItems wrapper.");
                foreach (var item in items)
                {
                    if (item.Type == JTokenType.Null) continue;
                    if (!TryReadLegacyItem(item, out var parsed, out var reason)) return Fail(reason);
                    if (parsed["itemStack"]?.Type == JTokenType.Null) continue;
                    if (parsed["itemStack"] is not JObject || parsed["remainingSeconds"]?.Type is not (JTokenType.Float or JTokenType.Integer))
                        return Fail("A legacy belt item lacks its itemStack or remainingSeconds.");
                    // 旧slotは1個で、0個は既存の欠損除去経路へ渡す。
                    // Legacy slots hold one item; zero-count entries retain the existing pruning path.
                    var count = parsed["itemStack"]["count"];
                    if (count?.Type != JTokenType.Integer || !int.TryParse(count.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount < 0 || 1 < amount)
                        return Fail("Legacy belt itemStack count must be the integer 0 or 1.");
                    // 空stackは既存どおり除去し、正数の識別子をロード前に検証する。
                    // Preserve empty-stack pruning and validate positive-count identities before loading.
                    var itemGuid = parsed["itemStack"]["itemGuid"];
                    if (0 < amount && (itemGuid == null || itemGuid.Type is not (JTokenType.String or JTokenType.Guid) || !Guid.TryParse(itemGuid.ToString(), out _)))
                        return Fail("Legacy positive-count belt itemStack itemGuid must be a GUID.");
                    // 任意GUIDの欠損は許可し、不正値はruntimeへ渡さない。
                    // Allow absent optional GUIDs and reject invalid values before runtime loading.
                    foreach (string key in new[] { "sourceConnectorGuid", "goalConnectorGuid" })
                    {
                        var connector = parsed[key];
                        if (connector != null && connector.Type != JTokenType.Null &&
                            (connector.Type is not (JTokenType.String or JTokenType.Guid) || !Guid.TryParse(connector.ToString(), out _)))
                            return Fail($"Legacy belt item {key} must be a GUID or null.");
                    }
                }
                if (needsWrapping) conversions.Add((state, items));
            }
            // 秒数とコネクターはロード時に実マスタで位置へ変換する。
            // Runtime loading maps seconds and connectors with the actual master.
            foreach (var conversion in conversions)
                conversion.State[BeltSaveKey] = new JObject { ["legacyItems"] = conversion.Items.DeepClone() };
            Debug.Log($"Migrated belt state V3 to V4: wrapped {conversions.Count} blocks.");
            return SaveMigrationStepResult.Converted(save);

            #region Internal
            bool TryReadLegacyItem(JToken token, out JObject item, out string reason)
            {
                item = token as JObject;
                reason = null;
                if (item != null) return true;
                if (token.Type != JTokenType.String) { reason = "Legacy belt item must contain JSON object text."; return false; }
                // 外部入力JSONの解析境界を隔離し、失敗は結果に残す。
                // Isolate the external input JSON parsing boundary and return an explicit failure.
                try { item = JObject.Parse((string)token); return true; }
                catch (JsonException exception)
                {
                    reason = $"Legacy belt item JSON could not be parsed: {exception.Message}";
                    Debug.LogWarning(reason);
                    return false;
                }
            }

            SaveMigrationStepResult Fail(string reason)
            {
                Debug.LogWarning($"Cannot migrate belt state V3 to V4: {reason}");
                return SaveMigrationStepResult.Failed(reason);
            }
            #endregion
        }

    }
}
