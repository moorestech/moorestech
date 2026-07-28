using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Detail
{
    /// <summary>
    ///     Detail1種類ぶんの配置設定。MapMaking BiomeDetailConfig.DetailEntry の移植
    ///     Placement settings for one kind of detail; ported from MapMaking's BiomeDetailConfig.DetailEntry
    /// </summary>
    public class DetailEntry
    {
        public DetailPrototypeConfig prototypeConfig;

        // 基本密度。バイオーム内での出現量の土台になる
        // Base density forming the foundation of how much this detail appears within the biome
        public float weight;

        // 算出密度がこの範囲外なら配置しない。狭い範囲でまばらな分布を作れる
        // Skip placement when the computed density falls outside this range, enabling sparse distributions
        public Vector2 weightRange;

        public int maxDensity;

        // 先行エントリが既に置いた場所を避ける
        // Avoid pixels where an earlier entry has already placed something
        public bool occludedByOthers;

        public DetailNoiseStack noiseStack;

        public DetailFilter slopeFilter;
        public DetailFilter curvatureFilter;
        public DetailFilter angleFilter;

        // 最近接のTree・Objectまでの距離で配置を制御する
        // Control placement by the distance to the nearest tree or object
        public DetailFilter treeDistanceFilter;
        public DetailFilter objectDistanceFilter;

        public DetailTextureFilter textureFilter;
    }
}
