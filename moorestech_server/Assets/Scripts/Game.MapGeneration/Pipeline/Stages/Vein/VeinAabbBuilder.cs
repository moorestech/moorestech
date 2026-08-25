using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置点中心の固定サイズAABBを作る（ADR-0023）。
    // Builds a fixed-size AABB centred on the point (ADR-0023).
    public static class VeinAabbBuilder
    {
        // 中心から全軸へ1セル張り出し、点中心の固定AABBにする。
        // Reaches one cell on every axis to form the fixed point-centred AABB.
        static readonly Vector3Int Extent = new(1, 1, 1);

        public static PlacedVein Build(string veinGuid, Vector3 worldPosition)
        {
            var center = Vector3Int.RoundToInt(worldPosition);
            return new PlacedVein
            {
                VeinGuid = veinGuid,
                Min = center - Extent,
                Max = center + Extent,
            };
        }

        // タイル内で生成可能な全AABBの範囲へ履歴が届くか判定する。
        // Tests whether history can reach the bounds of any AABB producible inside the tile.
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
