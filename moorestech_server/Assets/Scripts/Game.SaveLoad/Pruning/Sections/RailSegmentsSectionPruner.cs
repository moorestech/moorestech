using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Sections
{
    /// <summary>railSegments節: 除去したブロックに端を持つレール接続を除去する。レール種別guidはマスタ解決しないので欠損判定はしない</summary>
    /// <summary>The railSegments section: removes rail connections with an end on a pruned block; the rail type guid is never master-resolved, so no missing check is done</summary>
    public sealed class RailSegmentsSectionPruner : IRemovedBlockAwareSectionPruner
    {
        public string SaveSectionName => "railSegments";

        public MissingMasterSectionPruneResult Prune(JToken section, RemovedWorldBlockPositions removedWorldBlockPositions)
        {
            var result = new MissingMasterSectionPruneResult();
            if (removedWorldBlockPositions.IsEmpty) return result;
            if (section is not JArray railSegments)
            {
                Debug.LogWarning($"railSegments節が配列でないため、除去したブロックにつながるレール接続の除去を行いません。 type={section.Type}");
                return result;
            }

            // 判定を委譲し外した接続を結果へ積む
            // Delegate the check and stack removed connections onto the result
            foreach (var removedSegment in RemovedBlockRailCollateral.RemoveRailSegmentsTouching(railSegments, removedWorldBlockPositions)) result.RemovedRailSegments.Add(removedSegment);

            if (0 < result.RemovedRailSegments.Count)
            {
                Debug.LogWarning($"マスタ欠損で除去したブロックにつながるレール接続をセーブから除去しました。 count={result.RemovedRailSegments.Count}");
            }

            return result;
        }
    }
}
