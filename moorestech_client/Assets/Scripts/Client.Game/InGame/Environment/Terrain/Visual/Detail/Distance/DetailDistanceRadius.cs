using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Detail.Distance
{
    /// <summary>
    ///     距離フィルタが読み得る最大距離を求める。MapMaking SdfMapGenerator.ComputeMaxSearchRadius の移植で、
    ///     SDF距離マップの打ち切り半径と、タイル境界の外まで入力を広げるhalo幅の両方がこの値で決まる
    ///     Computes the farthest distance the filters can read; ported from MapMaking's SdfMapGenerator.ComputeMaxSearchRadius.
    ///     It sets both the SDF cutoff and the halo that widens the input past the tile boundary
    /// </summary>
    public static class DetailDistanceRadius
    {
        // 切り出しhaloは全バイオームで1つ。バイオームごとの半径から最大を採る式もこの半径の持ち主が持つ
        // One halo serves every biome, and the class that owns the radius formula owns taking the maximum over them too
        public static float MaxOverConfigs(IReadOnlyList<BiomeDetailConfig> detailConfigs)
        {
            var maxRadius = 0f;
            foreach (var detailConfig in detailConfigs)
                maxRadius = Mathf.Max(maxRadius, Mathf.Max(
                    ForTrees(detailConfig.entries), ForObjects(detailConfig.entries)));

            return maxRadius;
        }

        public static float ForTrees(DetailEntry[] entries)
        {
            var maxRadius = 0f;
            foreach (var entry in entries)
                maxRadius = Mathf.Max(maxRadius, entry.treeDistanceFilter.RequiredInputRange);

            return maxRadius;
        }

        public static float ForObjects(DetailEntry[] entries)
        {
            var maxRadius = 0f;
            foreach (var entry in entries)
                maxRadius = Mathf.Max(maxRadius, entry.objectDistanceFilter.RequiredInputRange);

            return maxRadius;
        }
    }
}
