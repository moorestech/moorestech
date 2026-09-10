using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 配置点中心の固定サイズAABBを作る（ADR-0023）。
    // Builds a fixed-size AABB centred on the point (ADR-0023).
    public static class VeinAabbBuilder
    {
        // 中心から各軸へ張り出す量。XZは3セル、Yは中心1セルのみの3x1x3。
        // 候補範囲を先に畳む側（TileCandidateAabbBounds）も同じ値で広げる必要があるので公開する。
        // The per-axis reach from the centre; XZ span three cells while Y stays a single centre cell, giving 3x1x3.
        // It is public because whoever folds candidates into bounds up front (TileCandidateAabbBounds) must widen by the same amount.
        public static readonly Vector3Int Extent = new(1, 0, 1);

        public static PlacedVein Build(string veinGuid, Vector3 worldPosition)
        {
            var center = Vector3Int.RoundToInt(worldPosition);
            return new PlacedVein(veinGuid, center - Extent, center + Extent);
        }

        // 候補AABBが一覧のどれかと重なるか。除外集合と同バッチの確定分を同じ述語で見る。
        // Whether the candidate AABB overlaps any listed vein; the excluded set and this batch's confirmed veins share this predicate.
        public static bool OverlapsAny(PlacedVein candidate, IReadOnlyList<PlacedVein> veins)
        {
            foreach (var vein in veins)
                if (candidate.Min.x <= vein.Max.x && vein.Min.x <= candidate.Max.x &&
                    candidate.Min.y <= vein.Max.y && vein.Min.y <= candidate.Max.y &&
                    candidate.Min.z <= vein.Max.z && vein.Min.z <= candidate.Max.z)
                    return true;
            return false;
        }
    }
}
