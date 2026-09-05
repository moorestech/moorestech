using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Tiling
{
    /// <summary>
    ///     1タイル内で生成しうる鉱脈AABBを全部包む範囲。到達判定をこの型へ閉じ、鉱脈側はタイル概念を知らないままにする。
    ///     候補中心は OreEntryPlacer がタイル矩形内へクランプしワールド整数へ丸めた点に限られ、この範囲導出はそれが前提。
    ///     Yは候補の高さが地形サンプルで決まり範囲を先に畳めないため見ない（XZだけで絞る）。
    ///     The bounds enclosing every vein AABB producible inside one tile; the reach test lives here so the vein side stays free of tile concepts.
    ///     Candidate centres are only the points OreEntryPlacer clamps into the tile rectangle and rounds to world integers, which this derivation assumes.
    ///     Y is left out because a candidate's height comes from a terrain sample and cannot be folded into bounds up front, so only XZ narrows the set.
    /// </summary>
    public readonly struct TileCandidateAabbBounds
    {
        private readonly int _minX;
        private readonly int _maxX;
        private readonly int _minZ;
        private readonly int _maxZ;

        private TileCandidateAabbBounds(int minX, int maxX, int minZ, int maxZ)
        {
            _minX = minX;
            _maxX = maxX;
            _minZ = minZ;
            _maxZ = maxZ;
        }

        // タイル矩形の整数点集合をAABBの張り出しぶん広げる。タイル寸法から一意に決まるので1タイルにつき1回で足りる。
        // Widens the tile rectangle's integer points by the AABB reach; it follows uniquely from the tile bounds, so once per tile is enough.
        public static TileCandidateAabbBounds From(TerrainDimensions dims)
        {
            var extent = VeinAabbBuilder.Extent;
            return new TileCandidateAabbBounds(
                Mathf.CeilToInt(dims.WorldOffsetX) - extent.x,
                Mathf.CeilToInt(dims.WorldOffsetX + dims.TerrainWidth) - 1 + extent.x,
                Mathf.CeilToInt(dims.WorldOffsetZ) - extent.z,
                Mathf.CeilToInt(dims.WorldOffsetZ + dims.TerrainLength) - 1 + extent.z);
        }

        // 過去タイルの確定AABBがこの範囲へ触れうるか。触れないなら以降どの候補とも重ならない。
        // Whether a confirmed AABB from an earlier tile can touch these bounds; if not, it overlaps no candidate here.
        public bool CanReach(PlacedVein history)
        {
            if (history.Max.x < _minX || _maxX < history.Min.x) return false;
            if (history.Max.z < _minZ || _maxZ < history.Min.z) return false;
            return true;
        }
    }
}
