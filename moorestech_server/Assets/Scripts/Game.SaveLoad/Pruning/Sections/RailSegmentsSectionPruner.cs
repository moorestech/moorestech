using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Sections
{
    /// <summary>railSegments節: 除去したブロックに端を持つレール接続を除去する。レール種別guidはマスタ解決しないので欠損判定はしない</summary>
    /// <summary>The railSegments section: removes rail connections with an end on a pruned block; the rail type guid is never master-resolved, so no missing check is done</summary>
    /// <summary>綴りはRailSegmentSaveDataのフィールド名（JsonProperty無し）に一致させる</summary>
    /// <summary>Spellings match the field names of RailSegmentSaveData, which carry no JsonProperty</summary>
    public sealed class RailSegmentsSectionPruner : IRemovedBlockAwareSectionPruner
    {
        private const string StartKey = "A";
        private const string EndKey = "B";

        public string SaveSectionName => "railSegments";

        // 残すとRailGraphDatastore.TryRestoreRailSegmentが端を解決できずfalseを返し、ロードで無音に消える
        // Left in place, RailGraphDatastore.TryRestoreRailSegment cannot resolve the end, returns false and the connection silently vanishes on load
        public MissingMasterSectionPruneResult Prune(JToken section, RemovedWorldBlockPositions removedWorldBlockPositions)
        {
            var result = new MissingMasterSectionPruneResult();
            if (removedWorldBlockPositions.IsEmpty) return result;
            if (section is not JArray railSegments)
            {
                Debug.LogWarning($"railSegments節が配列でないため、除去したブロックにつながるレール接続の除去を行いません。 type={section.Type}");
                return result;
            }

            // 退避してから外す。後日の返金はレール種別と長さからコストを引く
            // Archive before removing; a later refund derives the cost from the rail type and length
            foreach (var segment in railSegments.OfType<JObject>().ToList())
            {
                if (!removedWorldBlockPositions.ContainsConnectionDestination(segment[StartKey]) && !removedWorldBlockPositions.ContainsConnectionDestination(segment[EndKey])) continue;

                result.RemovedRailSegments.Add(segment.DeepClone());
                segment.Remove();
            }

            if (0 < result.RemovedRailSegments.Count)
            {
                Debug.LogWarning($"マスタ欠損で除去したブロックにつながるレール接続をセーブから除去しました。 count={result.RemovedRailSegments.Count}");
            }

            return result;
        }
    }
}
