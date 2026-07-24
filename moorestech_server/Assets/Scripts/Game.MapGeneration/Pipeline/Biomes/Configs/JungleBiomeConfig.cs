using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // ジャングルバイオームの高さ生成パラメータと配置サブ設定（実行時 POCO）。
    // Jungle biome height parameters and placement sub-configs (runtime POCO).
    public class JungleBiomeConfig
    {
        public float temperatureThreshold = 0.55f;
        public float humidityThreshold = 0.58f;
        public float warpStrength = 30f;
        public int warpOctaves = 1;
        public float terraceFrequency = 0.01f;
        public int terraceStepCount = 7;
        public float transitionSmoothing = 0.293f;
        public float cellHeightVariation = 0f;
        public float slopeWidth = 0.831f;
        public float slopeRepeat = 1.3f;
        public float slopeCoverage = 1f;
        public float surfaceDetailFrequency = 0.03f;
        public float surfaceDetailAmplitude = 1f;
        public float boundaryNoiseStrength = 40f;
        public float boundaryNoiseFrequency = 0.509f;
        public float boundaryNoiseSlopeThreshold = 16.2f;
        public float baseHeight = 0.05f;
        public float amplitude = 0.2f;
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
