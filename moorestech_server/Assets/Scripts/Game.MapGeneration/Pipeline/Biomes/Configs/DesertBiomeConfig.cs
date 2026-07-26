using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // 砂漠バイオームの高さ生成パラメータと配置サブ設定（実行時 POCO）。
    // Desert biome height parameters and placement sub-configs (runtime POCO).
    public class DesertBiomeConfig
    {
        public float temperatureThreshold = 0.42f;
        public float duneNoiseFrequency = 0.003f;
        public float duneAmplitude = 0.02f;
        public float canyonDepth = 0.6f;
        public float canyonFrequency = 0.001f;
        public int canyonOctaves = 4;
        public float cliffAmplitude = 0.22f;
        public float cliffFrequency = 0.0012f;
        public int cliffOctaves = 4;
        public float absSmoothing = 0.1f;
        public float baseHeight = 0.03f;
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
