using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators.Util
{
    /// <summary>
    /// SpatialGrid から Detail 解像度の距離マップ（float[,]）を生成する。
    /// Tree/Object 配置結果を距離マップ化し、DetailPlacementGenerator のフィルタ入力に使う。
    /// 行単位の Parallel.For でマルチコア並列化し、ブルートフォースと完全一致の結果を返す。
    /// </summary>
    public static class SdfMapGenerator
    {
        /// <summary>
        /// 全ピクセルの最近接点までの距離（ワールド単位）を並列計算する。
        /// maxSearchRadius 内に点がなければその値で打ち切る。
        /// 行単位で並列化し、各ピクセルの計算は完全に独立のため結果はシングルスレッドと一致する。
        /// </summary>
        public static float[,] Generate(
            SpatialGrid grid, int resolution,
            float terrainWidth, float terrainLength, float maxSearchRadius)
        {
            if (grid == null || grid.Count == 0)
                return null;

            var map = new float[resolution, resolution];
            int res = resolution; // ラムダキャプチャ用ローカル
            float tw = terrainWidth;
            float tl = terrainLength;
            float maxR = maxSearchRadius;

            // 行単位で並列化。各行の全ピクセルは独立かつ SpatialGrid は読み取り専用
            // 浮動小数点演算はシングルスレッド版と完全同一の式を使用
            Parallel.For(0, res, z =>
            {
                for (int x = 0; x < res; x++)
                {
                    float worldX = (float)x / (res - 1) * tw;
                    float worldZ = (float)z / (res - 1) * tl;
                    map[z, x] = grid.FindMinDistance(worldX, worldZ, maxR);
                }
            });

            return map;
        }

        /// <summary>
        /// 旧アルゴリズム（シングルスレッド）。リファレンス検証用に残す。
        /// </summary>
        public static float[,] GenerateSingleThread(
            SpatialGrid grid, int resolution,
            float terrainWidth, float terrainLength, float maxSearchRadius)
        {
            if (grid == null || grid.Count == 0)
                return null;

            var map = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float worldX = (float)x / (resolution - 1) * terrainWidth;
                float worldZ = (float)z / (resolution - 1) * terrainLength;
                map[z, x] = grid.FindMinDistance(worldX, worldZ, maxSearchRadius);
            }
            return map;
        }

        /// <summary>
        /// 有効な距離フィルタの最大探索距離を算出する。
        /// range.y + smoothness.y の最大値を返す（フィルタ無効時は 0）。
        /// </summary>
        public static float ComputeMaxSearchRadius(
            Config.BiomeDetailConfig.DetailEntry[] entries, bool forTree)
        {
            float maxRadius = 0f;
            if (entries == null) return maxRadius;

            foreach (var entry in entries)
            {
                var filter = forTree ? entry.treeDistanceFilter : entry.objectDistanceFilter;
                if (filter == null || !filter.enabled) continue;
                float needed = filter.range.y + filter.smoothness.y;
                if (needed > maxRadius) maxRadius = needed;
            }
            return maxRadius;
        }
    }
}
