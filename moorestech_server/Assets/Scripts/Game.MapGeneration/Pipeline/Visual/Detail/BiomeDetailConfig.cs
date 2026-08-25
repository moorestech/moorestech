namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    /// <summary>
    ///     バイオーム1つぶんのDetail配置設定。MapMaking BiomeDetailConfig の移植
    ///     Detail placement settings for one biome; ported from MapMaking's BiomeDetailConfig
    /// </summary>
    public class BiomeDetailConfig
    {
        public DetailEntry[] entries;

        // 閾値未満の画素を棄却
        // Reject pixels below this threshold
        public float filterRejectThreshold;

        // 境界近傍にはDetailを置かない
        // Keep details away from biome borders
        public float borderMargin;
    }
}
