using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置点中心の固定サイズAABBを作る（ADR-0023）。
    // Builds a fixed-size AABB centred on the point (ADR-0023).
    public static class VeinAabbBuilder
    {
        // 中心から各軸へ張り出す量。inclusive なので Max-Min = 2・実体は1辺3セル。
        // The per-axis reach from the centre; inclusive bounds make Max-Min = 2 while the body covers three cells per edge.
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
