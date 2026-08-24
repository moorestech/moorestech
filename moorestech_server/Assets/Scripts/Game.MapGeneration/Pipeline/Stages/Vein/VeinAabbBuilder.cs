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
    }
}
