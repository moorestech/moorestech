using System;
using System.Collections.Generic;
using System.Globalization;
using Game.SaveLoad.Interface;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning
{
    /// <summary>除去後のセーブと、取り除いた実体。実体は後から置換・返金を足すときの入力になる</summary>
    /// <summary>The pruned save plus the removed entities, which a later replace/refund migration consumes</summary>
    public sealed class MissingMasterPruneOutcome
    {
        private readonly MissingMasterSectionPruneResult _removed = new();

        public JObject Save { get; }
        public MissingMasterPruneReport Report { get; }

        public MissingMasterPruneOutcome(JObject save, IReadOnlyList<MissingMasterSectionPruneResult> sectionResults)
        {
            Save = save;

            // 節ごとの結果を種別ごとの1本の配列へ畳む
            // Fold the per-section results into one array per kind
            foreach (var sectionResult in sectionResults)
            {
                Append(_removed.RemovedBlocks, sectionResult.RemovedBlocks);
                Append(_removed.EmptiedItemStacks, sectionResult.EmptiedItemStacks);
                Append(_removed.NeutralizedConnectionMaterials, sectionResult.NeutralizedConnectionMaterials);
                Append(_removed.RemovedResearchGuids, sectionResult.RemovedResearchGuids);
                Append(_removed.RemovedUnlockStates, sectionResult.RemovedUnlockStates);
                Append(_removed.RemovedTrainUnits, sectionResult.RemovedTrainUnits);
            }

            // 裸guid（燃焼中の燃料スロット等）もEmptiedItemStackCountに含める。通知の文言は「枠数」ではなく「取り除いた件数」を指す
            // Bare-guid entries (e.g. a burning fuel slot) count toward EmptiedItemStackCount; the notice means "items removed", not "slots emptied"
            Report = new MissingMasterPruneReport(_removed.RemovedBlocks.Count, _removed.EmptiedItemStacks.Count, _removed.RemovedResearchGuids.Count);
        }

        // 通知件数に入らない種別だけを除去した場合もファイルへ残す。プレイヤーへの通知はReport側の件数だけで決める
        // Removals outside the notice counts still deserve a file; the player-facing notice is decided by Report's counts only
        public bool HasRemoval => Report.HasRemoval
                                  || 0 < _removed.NeutralizedConnectionMaterials.Count
                                  || 0 < _removed.RemovedUnlockStates.Count
                                  || 0 < _removed.RemovedTrainUnits.Count;

        // 実世界の日時そのものを記録する用途なのでDateTimeでよい（AGENTS.mdの例外）
        // Recording a real-world timestamp is the sanctioned DateTime use (AGENTS.md exception)
        // 書式はカルチャに左右させない。和暦・仏暦では年が、一部ロケールでは時刻区切りが化ける
        // The format must not follow the culture: the year shifts under non-Gregorian calendars and ':' is a culture separator
        public JObject ToPrunedJson(DateTime utcNow)
        {
            return new JObject
            {
                ["prunedAt"] = utcNow.ToString("yyyy-MM-dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture),
                // 保持している配列そのものをぶら下げると2回目の呼び出しで1回目の結果が壊れる
                // Attaching the held arrays themselves would break the first result on a second call
                ["blocks"] = _removed.RemovedBlocks.DeepClone(),
                ["items"] = _removed.EmptiedItemStacks.DeepClone(),
                ["connectionMaterials"] = _removed.NeutralizedConnectionMaterials.DeepClone(),
                ["research"] = _removed.RemovedResearchGuids.DeepClone(),
                ["unlockStates"] = _removed.RemovedUnlockStates.DeepClone(),
                ["trainUnits"] = _removed.RemovedTrainUnits.DeepClone(),
            };
        }

        private static void Append(JArray destination, JArray source)
        {
            foreach (var entry in source) destination.Add(entry.DeepClone());
        }
    }
}
