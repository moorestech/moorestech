using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Detail
{
    /// <summary>
    ///     Detail1ピクセル分の密度を全フィルタ通過で算出する。MapMaking DetailPlacementGenerator の内側ループの移植
    ///     Computes one detail pixel's density through the whole filter chain; ported from MapMaking's inner loop
    /// </summary>
    public static class DetailDensitySampler
    {
        // どれか1つでもフィルタが棄却閾値を下回ったらfalse。順序と早期棄却は移植元と同一
        // Returns false as soon as any filter falls under the reject threshold, preserving the source's order and early exits
        public static bool TryEvaluate(DetailEntry entry, in DetailSampleContext context, int x, int z, out float density)
        {
            density = 0f;

            // detail座標をheightmap座標へ変換する。両者は解像度が1違う
            // Convert detail coordinates to heightmap coordinates; the two resolutions differ by one
            var heightmapX = Mathf.Clamp(
                Mathf.RoundToInt((float)x / (context.DetailResolution - 1) * (context.HeightmapResolution - 1)), 0, context.HeightmapResolution - 1);
            var heightmapZ = Mathf.Clamp(
                Mathf.RoundToInt((float)z / (context.DetailResolution - 1) * (context.HeightmapResolution - 1)), 0, context.HeightmapResolution - 1);

            // 領域外と境界近傍を除外
            // Exclude outside and border cells
            if (!context.Mask[heightmapZ, heightmapX]) return false;
            if (0f < context.BorderMarginPixels
                && BiomeMaskBuilder.IsNearMaskEdge(context.Mask, heightmapX, heightmapZ, context.HeightmapResolution, context.BorderMarginPixels))
                return false;

            // ノイズはワールド座標で引く。SplatmapJob と同じくタイル原点を足してから正規化位置を進める
            // The noise is drawn in world space: as in SplatmapJob the tile origin comes first, then the normalized step
            var worldX = context.WorldOffsetX + (float)x / context.DetailResolution * context.TerrainWidth;
            var worldZ = context.WorldOffsetZ + (float)z / context.DetailResolution * context.TerrainLength;
            var rejectThreshold = context.FilterRejectThreshold;

            // エントリ重みへ各フィルタを乗算
            // Multiply each filter into entry weight
            var computed = entry.weight;

            var slopeFactor = entry.slopeFilter.Evaluate(context.Slopes[heightmapZ, heightmapX], worldX, worldZ, context.NoiseOffsets);
            if (slopeFactor < rejectThreshold) return false;
            computed *= slopeFactor;

            if (entry.curvatureFilter.enabled && context.Curvature != null)
            {
                var curvatureFactor = entry.curvatureFilter.Evaluate(context.Curvature[heightmapZ, heightmapX], worldX, worldZ, context.NoiseOffsets);
                if (curvatureFactor < rejectThreshold) return false;
                computed *= curvatureFactor;
            }

            if (entry.angleFilter.enabled && context.Azimuth != null)
            {
                var angleFactor = entry.angleFilter.Evaluate(context.Azimuth[heightmapZ, heightmapX], worldX, worldZ, context.NoiseOffsets);
                if (angleFactor < rejectThreshold) return false;
                computed *= angleFactor;
            }

            // 距離フィルタはdetail解像度の距離場を直接引く（heightmap座標へは変換しない）
            // The distance filters index the distance fields at detail resolution without converting to heightmap coordinates
            if (entry.treeDistanceFilter.enabled && context.TreeDistanceMap != null)
            {
                var treeFactor = entry.treeDistanceFilter.Evaluate(context.TreeDistanceMap[z, x], worldX, worldZ, context.NoiseOffsets);
                if (treeFactor < rejectThreshold) return false;
                computed *= treeFactor;
            }

            if (entry.objectDistanceFilter.enabled && context.ObjectDistanceMap != null)
            {
                var objectFactor = entry.objectDistanceFilter.Evaluate(context.ObjectDistanceMap[z, x], worldX, worldZ, context.NoiseOffsets);
                if (objectFactor < rejectThreshold) return false;
                computed *= objectFactor;
            }

            if (entry.textureFilter.enabled && context.Splatmap != null && context.TerrainLayers != null)
            {
                // detail座標をsplatmap座標へ変換してからレイヤー重みを引く
                // Convert detail coordinates to splatmap coordinates before reading the layer weights
                var splatX = Mathf.Clamp(
                    Mathf.RoundToInt((float)x / (context.DetailResolution - 1) * (context.SplatResolution - 1)), 0, context.SplatResolution - 1);
                var splatZ = Mathf.Clamp(
                    Mathf.RoundToInt((float)z / (context.DetailResolution - 1) * (context.SplatResolution - 1)), 0, context.SplatResolution - 1);
                var textureFactor = entry.textureFilter.Evaluate(context.Splatmap, splatZ, splatX, context.TerrainLayers);
                if (textureFactor < rejectThreshold) return false;
                computed *= textureFactor;
            }

            if (entry.noiseStack.IsActive)
                computed *= entry.noiseStack.Sample(worldX, worldZ, context.NoiseOffsets);

            // weightRangeの外は棄却閾値と無関係に落とす。狭い帯でまばらな分布を作る仕組み
            // Values outside weightRange are dropped regardless of the reject threshold, producing sparse bands
            if (computed < entry.weightRange.x || entry.weightRange.y < computed) return false;

            density = computed;
            return true;
        }
    }
}
