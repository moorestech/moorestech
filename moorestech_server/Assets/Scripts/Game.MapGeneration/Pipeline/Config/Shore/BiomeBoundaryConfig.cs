namespace Game.MapGeneration.Pipeline.Config
{
    // バイオーム境界・ブレンドの共通設定。
    // Common biome boundary/blend config.
    public class BiomeBoundaryConfig
    {
        public float textureBlendStrength = 0.6f;
        public float heightBlendFastPathThreshold = 0.95f;
        public float heightBlendMinWeight = 0.01f;
        public int blurRadiusDivisor = 2;
        public float boundaryNoiseSmoothstepWidth = 15f;
        public float boundaryNoiseMidWeight = 0.7f;
        public float boundaryNoiseHighWeight = 0.3f;
    }
}
