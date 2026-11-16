using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Migration.Steps.Shared;
using Game.SaveLoad.Pruning;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// worldの向き読替・寝かせ・重なり撤去と巻き添え除去
    /// Renumber world directions, lay flat, remove overlaps and collateral
    /// </summary>
    internal sealed class VerticalDirectionWorldRewriter : ISaveMigrationPart
    {
        private const string WorldKey = "world";
        private const string RailSegmentsKey = "railSegments";
        private const string TrainUnitsKey = "trainUnits";

        private JArray _world;
        private JArray _railSegments;
        private JArray _trainUnits;
        private List<LegacyBlockEntry> _entries;

        public bool TryPrepare(JObject save, out string failureReason)
        {
            if (!(save[WorldKey] is JArray world)) return MigrationSaveJson.Reject($"セーブのworldが配列ではありません。 type={save[WorldKey]?.Type}", out failureReason);

            // 節が無い/nullは飛ばし、配列以外は拒否
            // Skip absent/null sections; refuse non-arrays
            if (!TryGetOptionalArray(save, RailSegmentsKey, out var railSegments, out failureReason)) return false;
            if (!TryGetOptionalArray(save, TrainUnitsKey, out var trainUnits, out failureReason)) return false;

            var entries = new List<LegacyBlockEntry>();
            foreach (var blockToken in world)
            {
                var label = $"world要素 instanceId={(blockToken as JObject)?["instanceId"]}";
                if (!LegacyBlockEntry.TryRead(blockToken, "X", "Y", "Z", label, out var entry, out failureReason)) return false;
                entries.Add(entry);
            }

            // 重なる縦置き同士は instanceId の小さい方を残すため、その順に並べる（読めない instanceId は最後）
            // Order by instanceId so the smaller one survives when vertical blocks overlap each other (unreadable ids go last)
            _entries = entries.OrderBy(entry => InstanceIdOrMax(entry.Block)).ToList();
            _world = world;
            _railSegments = railSegments;
            _trainUnits = trainUnits;
            failureReason = null;
            return true;

            #region Internal

            // 無い/nullはnull、配列以外は拒否
            // Null when absent/null; refuse non-arrays
            bool TryGetOptionalArray(JObject save, string key, out JArray array, out string failureReason)
            {
                array = null;
                failureReason = null;
                var token = save[key];
                if (MigrationSaveJson.IsAbsentOrNull(token)) return true;
                if (!(token is JArray found)) return MigrationSaveJson.Reject($"セーブの{key}が配列ではありません。 type={token.Type}", out failureReason);
                array = found;
                return true;
            }

            long InstanceIdOrMax(JObject block)
            {
                var token = block["instanceId"];
                return token != null && token.Type == JTokenType.Integer ? token.Value<long>() : long.MaxValue;
            }

            #endregion
        }

        public string ApplyAndSummarize()
        {
            var outcome = VerticalBlockLayFlattener.LayFlat(_entries);
            var removedBlocks = RemoveOverlappingBlocks();
            var (removedRailSegmentCount, removedTrainUnitCount) = RemoveCollateral();

            return $"縦向きを寝かせた={outcome.LaidFlatCount}件 壁パネルへ置換={outcome.WallPanelCount}件 重なりで撤去={removedBlocks.Count}件 巻き添えのレール={removedRailSegmentCount}件 列車={removedTrainUnitCount}件";

            #region Internal

            // 撤去ブロックを外し件数と位置をログ
            // Remove blocks and log count and positions
            JArray RemoveOverlappingBlocks()
            {
                var removed = new JArray();
                foreach (var entry in outcome.Removed)
                {
                    removed.Add(entry.Block.DeepClone());
                    entry.Block.Remove();
                }

                if (removed.Count == 0) return removed;
                var targets = string.Join(", ", removed.Select(block => $"{block["blockGuid"]}@({block["X"]},{block["Y"]},{block["Z"]})"));
                Debug.LogWarning($"縦向きを寝かせると他のブロックと重なるため撤去しました。 件数={removed.Count} 対象=[{targets}]");
                return removed;
            }

            // 撤去ブロックのレール接続と列車を外す
            // Remove rail connections and trains of removed blocks
            (int railSegmentCount, int trainUnitCount) RemoveCollateral()
            {
                if (removedBlocks.Count == 0) return (0, 0);
                var removedPositions = new RemovedWorldBlockPositions(removedBlocks);
                var railSegmentCount = _railSegments == null ? 0 : RemovedBlockRailCollateral.RemoveRailSegmentsTouching(_railSegments, removedPositions).Count;
                var trainUnitCount = _trainUnits == null ? 0 : RemovedBlockRailCollateral.RemoveTrainUnitsOnRemovedBlocks(_trainUnits, removedPositions).Count;
                return (railSegmentCount, trainUnitCount);
            }

            #endregion
        }
    }
}
