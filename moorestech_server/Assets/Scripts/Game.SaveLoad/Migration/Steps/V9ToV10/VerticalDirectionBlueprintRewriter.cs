using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// ブループリント内の向き読替・縦置き寝かせ・添字付替え
    /// Renumber blueprint directions, lay vertical blocks flat, remap indices
    /// </summary>
    internal sealed class VerticalDirectionBlueprintRewriter : ISaveMigrationPart
    {
        private const string BlueprintsKey = "blueprints";
        private static readonly string[] LineKeys = BlueprintBlockRemoval.LineKeys;
        private const string BlockIndexAKey = BlueprintBlockRemoval.BlockIndexAKey;
        private const string BlockIndexBKey = BlueprintBlockRemoval.BlockIndexBKey;

        private readonly List<(JObject blueprint, List<LegacyBlockEntry> entries)> _targets = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            failureReason = null;
            var blueprintsToken = save[BlueprintsKey];
            if (MigrationSaveJson.IsAbsentOrNull(blueprintsToken)) return true;
            if (!(blueprintsToken is JArray blueprints)) return MigrationSaveJson.Reject($"セーブのblueprintsが配列ではありません。 type={blueprintsToken.Type}", out failureReason);

            foreach (var blueprintToken in blueprints)
            {
                if (!(blueprintToken is JObject blueprint)) return MigrationSaveJson.Reject($"blueprints要素がオブジェクトではありません。 type={blueprintToken.Type}", out failureReason);
                if (!TryReadBlueprint(blueprint, out var entries, out failureReason)) return false;
                _targets.Add((blueprint, entries));
            }

            return true;

            #region Internal

            // 形と添字範囲を検証しブロックを添字順に読む
            // Validate shapes and index ranges, read blocks in index order
            bool TryReadBlueprint(JObject blueprint, out List<LegacyBlockEntry> entries, out string failureReason)
            {
                entries = new List<LegacyBlockEntry>();
                var name = blueprint["name"];
                if (!(blueprint["blocks"] is JArray blocks)) return MigrationSaveJson.Reject($"ブループリントのblocksが配列ではありません。 blueprint={name} type={blueprint["blocks"]?.Type}", out failureReason);

                for (var i = 0; i < blocks.Count; i++)
                {
                    if (!LegacyBlockEntry.TryRead(blocks[i], "offsetX", "offsetY", "offsetZ", $"ブループリント内ブロック blueprint={name} index={i}", out var entry, out failureReason)) return false;
                    entries.Add(entry);
                }

                foreach (var lineKey in LineKeys)
                {
                    if (!(blueprint[lineKey] is JArray lines)) return MigrationSaveJson.Reject($"ブループリントの{lineKey}が配列ではありません。 blueprint={name} type={blueprint[lineKey]?.Type}", out failureReason);
                    foreach (var line in lines)
                    {
                        if (line is JObject lineObject && IsIndexInRange(lineObject[BlockIndexAKey], blocks.Count) && IsIndexInRange(lineObject[BlockIndexBKey], blocks.Count)) continue;
                        return MigrationSaveJson.Reject($"ブループリントの{lineKey}の添字が読めないか範囲外です。 blueprint={name} line={line.ToString(Newtonsoft.Json.Formatting.None)} blocks={blocks.Count}", out failureReason);
                    }
                }

                failureReason = null;
                return true;
            }

            bool IsIndexInRange(JToken token, int count)
            {
                if (token == null || token.Type != JTokenType.Integer) return false;
                var index = token.Value<long>();
                return 0 <= index && index < count;
            }

            #endregion
        }

        public string ApplyAndSummarize()
        {
            if (_targets.Count == 0) return "ブループリント 対象なし";

            var laidFlatCount = 0;
            var wallPanelCount = 0;
            var removedCount = 0;
            foreach (var (blueprint, entries) in _targets)
            {
                // 占有はブループリント内だけで計算し、重なりは添字の小さい方を残す
                // Occupancy is computed within the blueprint only, and the lower index survives an overlap
                var outcome = VerticalBlockLayFlattener.LayFlat(entries);
                laidFlatCount += outcome.LaidFlatCount;
                wallPanelCount += outcome.WallPanelCount;
                if (outcome.Removed.Count == 0) continue;

                RemoveBlocksAndRemapLines(blueprint, entries, outcome.Removed);
                removedCount += outcome.Removed.Count;
                Debug.LogWarning($"ブループリント内の縦向きを寝かせると重なるため撤去しました。 blueprint={blueprint["name"]} 件数={outcome.Removed.Count}");
            }

            return $"ブループリントの縦向きを寝かせた={laidFlatCount}件 壁パネルへ置換={wallPanelCount}件 重なりで撤去={removedCount}件";

            #region Internal

            // 撤去ブロックを外し、端が消えた電線・チェーンも除いて添字を詰める
            // Drop removed blocks and wires/chains with a removed end, compact indices
            void RemoveBlocksAndRemapLines(JObject blueprint, List<LegacyBlockEntry> entries, List<LegacyBlockEntry> removed)
            {
                var removedSet = new HashSet<LegacyBlockEntry>(removed);
                var removedIndices = Enumerable.Range(0, entries.Count).Where(index => removedSet.Contains(entries[index])).ToList();
                BlueprintBlockRemoval.RemoveAt(blueprint, removedIndices);
            }

            #endregion
        }
    }
}
