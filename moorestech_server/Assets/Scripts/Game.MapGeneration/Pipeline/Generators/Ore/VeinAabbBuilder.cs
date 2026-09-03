using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 配置点中心の固定サイズAABBを作る（ADR-0023）。
    // Builds a fixed-size AABB centred on the point (ADR-0023).
    public static class VeinAabbBuilder
    {
        // 中心から各軸へ張り出す量。XZは3セル、Yは中心1セルのみの3x1x3。
        // The per-axis reach from the centre; XZ span three cells while Y stays a single centre cell, giving 3x1x3.
        static readonly Vector3Int Extent = new(1, 0, 1);

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

        // タイル内で生成可能な全AABBの範囲へ履歴が届くか判定する。
        // 候補中心はOreEntryPlacerがタイル矩形内へクランプしワールド整数へ丸めた点に限られ、この範囲導出はそれが前提。
        // Tests whether history can reach the bounds of any AABB producible inside the tile.
        // Candidate centres are only the points OreEntryPlacer clamps into the tile rectangle and rounds to world integers, which this range derivation assumes.
        internal static bool CanOverlapAnyCandidateInTile(
            PlacedVein history, float tileWorldOffsetX, float tileWorldOffsetZ,
            float tileWidth, float tileLength)
        {
            int possibleMinX = Mathf.CeilToInt(tileWorldOffsetX) - Extent.x;
            int possibleMaxX = Mathf.CeilToInt(tileWorldOffsetX + tileWidth) - 1 + Extent.x;
            int possibleMinZ = Mathf.CeilToInt(tileWorldOffsetZ) - Extent.z;
            int possibleMaxZ = Mathf.CeilToInt(tileWorldOffsetZ + tileLength) - 1 + Extent.z;

            if (history.Max.x < possibleMinX || possibleMaxX < history.Min.x) return false;
            if (history.Max.z < possibleMinZ || possibleMaxZ < history.Min.z) return false;
            return true;
        }
    }
}
