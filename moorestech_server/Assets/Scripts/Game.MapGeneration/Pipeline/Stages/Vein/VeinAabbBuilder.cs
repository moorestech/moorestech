using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
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
            return new PlacedVein
            {
                VeinGuid = veinGuid,
                Min = center - Extent,
                Max = center + Extent,
            };
        }
    }
}
