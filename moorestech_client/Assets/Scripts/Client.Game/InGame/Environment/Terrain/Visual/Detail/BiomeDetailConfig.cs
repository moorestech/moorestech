namespace Client.Game.InGame.Environment.Terrain.Visual.Detail
{
    /// <summary>
    ///     バイオーム1つぶんのDetail配置設定。MapMaking BiomeDetailConfig の移植
    ///     Detail placement settings for one biome; ported from MapMaking's BiomeDetailConfig
    /// </summary>
    public class BiomeDetailConfig
    {
        public DetailEntry[] entries;

        // フィルタ値がこれを下回った時点でそのピクセルを棄却する
        // A pixel is rejected as soon as a filter value drops below this threshold
        public float filterRejectThreshold;

        // バイオーム境界からこの距離(m)以内にはDetailを置かない
        // No detail is placed within this distance (in meters) of the biome boundary
        public float borderMargin;
    }
}
