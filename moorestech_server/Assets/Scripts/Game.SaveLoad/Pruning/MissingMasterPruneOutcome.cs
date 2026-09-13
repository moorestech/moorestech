using System;
using System.Globalization;
using Game.SaveLoad.Interface;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning
{
    /// <summary>除去後のセーブと、取り除いた実体。実体は後から置換・返金を足すときの入力になる</summary>
    /// <summary>The pruned save plus the removed entities, which a later replace/refund migration consumes</summary>
    public sealed class MissingMasterPruneOutcome
    {
        private readonly JArray _removedBlocks;
        private readonly JArray _removedItemStacks;
        private readonly JArray _removedResearchGuids;

        public JObject Save { get; }
        public MissingMasterPruneReport Report { get; }

        public MissingMasterPruneOutcome(JObject save, JArray removedBlocks, JArray removedItemStacks, JArray removedResearchGuids)
        {
            Save = save;
            _removedBlocks = removedBlocks;
            _removedItemStacks = removedItemStacks;
            _removedResearchGuids = removedResearchGuids;
            Report = new MissingMasterPruneReport(removedBlocks.Count, removedItemStacks.Count, removedResearchGuids.Count);
        }

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
                ["blocks"] = _removedBlocks.DeepClone(),
                ["items"] = _removedItemStacks.DeepClone(),
                ["research"] = _removedResearchGuids.DeepClone(),
            };
        }
    }
}
