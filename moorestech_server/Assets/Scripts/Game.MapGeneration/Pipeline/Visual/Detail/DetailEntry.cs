using Game.MapGeneration.Facade;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    /// <summary>
    ///     Detail1種類ぶんの配置設定。MapMaking BiomeDetailConfig.DetailEntry の移植
    ///     Placement settings for one kind of detail; ported from MapMaking's BiomeDetailConfig.DetailEntry
    /// </summary>
    public class DetailEntry
    {
        public DetailPrototypeSpec prototypeConfig;

        // バイオーム内の基本密度
        // Base density within the biome
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

        // 最近接物との距離で配置制御
        // Control placement by nearest-object distance
        public DetailFilter treeDistanceFilter;
        public DetailFilter objectDistanceFilter;

        public DetailTextureFilter textureFilter;
    }
}
