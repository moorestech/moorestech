using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 鉱脈AABBを配置点を中心とした固定サイズで作る。移植元MapMakingのbounds(size 2 / center 0)と同じ式（ADR-0023）。
    // Builds a vein AABB as a fixed size centred on its placement point, matching MapMaking's bounds (size 2, centre 0) (ADR-0023).
    public static class VeinAabbBuilder
    {
        // 中心から各軸へ張り出す量。Min/Max は inclusive 判定なので1辺3セルを覆う。
        // The per-axis reach from the centre; Min/Max are inclusive so one edge covers three cells.
        public static readonly Vector3Int Extent = new(1, 1, 1);

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
