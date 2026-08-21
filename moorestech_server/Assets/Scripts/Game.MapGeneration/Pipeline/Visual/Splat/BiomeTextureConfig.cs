using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Visual.Splat
{
    /// <summary>
    ///     バイオーム1つぶんのテクスチャ合成設定。MapMaking BiomeTextureConfig の移植
    ///     Texture composition settings for one biome; ported from MapMaking's BiomeTextureConfig
    /// </summary>
    public class BiomeTextureConfig
    {
        public TextureEntry[] entries;
    }

    /// <summary>
    ///     1レイヤー分の合成条件。TerrainLayer参照はアドレス文字列に置き換わっている
    ///     Blend conditions for one layer; the TerrainLayer reference is replaced by an address string
    /// </summary>
    public class TextureEntry
    {
        public string layerAddressablePath;
        public float weight;

        // 傾斜フィルタ: 崖面テクスチャの切り替え
        // Slope filter: swaps in the cliff-face texture
        public bool useSlopeFilter;
        public float slopeMin;
        public float slopeMax;
        public float slopeSmoothness;

        // 高度依存のテクスチャ条件
        // Height-based texture condition
        public bool useHeightFilter;
        public float heightMin;
        public float heightMax;
        public float heightSmoothness;

        // 曲率依存のテクスチャ条件
        // Curvature-based texture condition
        public bool useCurvatureFilter;
        public float curvatureMin;
        public float curvatureMax;
        public float curvatureSmoothness;

        // ノイズ変調。MapNoiseType の序数として SplatmapJob へ渡る
        // Noise modulation, handed to SplatmapJob as the MapNoiseType ordinal
        public MapNoiseType noiseType;
        public float noiseFrequency;
        public float noiseAmplitude;
    }
}
