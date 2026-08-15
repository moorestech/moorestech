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
        public static float ForTrees(DetailEntry[] entries)
        {
            var maxRadius = 0f;
            foreach (var entry in entries)
                maxRadius = Mathf.Max(maxRadius, RequiredRadius(entry.treeDistanceFilter));

            return maxRadius;
        }

        public static float ForObjects(DetailEntry[] entries)
        {
            var maxRadius = 0f;
            foreach (var entry in entries)
                maxRadius = Mathf.Max(maxRadius, RequiredRadius(entry.objectDistanceFilter));

            return maxRadius;
        }

        // 上限側の減衰の裾までが必要範囲。range.yで切ると裾の内側の木を見落とし、境界画素だけ密度が跳ねる
        // The upper falloff tail is part of the needed range; cutting at range.y misses trees inside it and spikes the edge pixels
        private static float RequiredRadius(DetailFilter filter)
        {
            if (!filter.enabled) return 0f;

            return filter.range.y + filter.smoothness.y;
        }
    }
}
