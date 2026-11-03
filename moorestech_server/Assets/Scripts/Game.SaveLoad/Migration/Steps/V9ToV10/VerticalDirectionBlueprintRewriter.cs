using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 各ブループリント内のブロックの向きを水平4方向の番号へ読み替え、縦置きを寝かせる。重なるものは撤去し、電線・チェーンの添字を付け替える
    /// Renumbers each blueprint block's direction to the four horizontal headings and lays vertical blocks flat; overlapping ones are removed and wire/chain indices are remapped
    /// </summary>
    internal sealed class VerticalDirectionBlueprintRewriter : ISaveMigrationPart
    {
        private const string BlueprintsKey = "blueprints";
        private static readonly string[] LineKeys = { "wires", "chains" };
        private const string BlockIndexAKey = "blockIndexA";
        private const string BlockIndexBKey = "blockIndexB";

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
        }

        // blocks・wires・chains の形と添字の範囲を確かめ、ブロックを添字順に読む
        // Check the shapes of blocks/wires/chains and the index ranges, then read blocks in index order
        private static bool TryReadBlueprint(JObject blueprint, out List<LegacyBlockEntry> entries, out string failureReason)
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

        // 撤去したブロックを blocks から外し、電線・チェーンは端が撤去されたものを消して残りの添字を詰める
        // Drop removed blocks from blocks, delete wires/chains with a removed end, and compact the remaining indices
        private static void RemoveBlocksAndRemapLines(JObject blueprint, List<LegacyBlockEntry> entries, List<LegacyBlockEntry> removed)
        {
            var removedSet = new HashSet<LegacyBlockEntry>(removed);
            var newIndexByOldIndex = new Dictionary<int, int>();
            for (var oldIndex = 0; oldIndex < entries.Count; oldIndex++)
            {
                if (removedSet.Contains(entries[oldIndex])) continue;
                newIndexByOldIndex[oldIndex] = newIndexByOldIndex.Count;
            }

            foreach (var entry in removed) entry.Block.Remove();

            foreach (var lineKey in LineKeys)
            {
                foreach (var line in ((JArray)blueprint[lineKey]).OfType<JObject>().ToList())
                {
                    var hasA = newIndexByOldIndex.TryGetValue(line[BlockIndexAKey].Value<int>(), out var newA);
                    var hasB = newIndexByOldIndex.TryGetValue(line[BlockIndexBKey].Value<int>(), out var newB);
                    if (!hasA || !hasB)
                    {
                        line.Remove();
                        continue;
                    }

                    line[BlockIndexAKey] = newA;
                    line[BlockIndexBKey] = newB;
                }
            }
        }

        private static bool IsIndexInRange(JToken token, int count)
        {
            if (token == null || token.Type != JTokenType.Integer) return false;
            var index = token.Value<long>();
            return 0 <= index && index < count;
        }
    }
}
