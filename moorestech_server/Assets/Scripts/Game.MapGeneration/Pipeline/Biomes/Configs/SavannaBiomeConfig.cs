using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // サバンナバイオームの高さ生成パラメータと配置サブ設定（実行時 POCO）。
    // Savanna biome height parameters and placement sub-configs (runtime POCO).
    public class SavannaBiomeConfig
    {
        public float temperatureThreshold = 0.55f;
        public float frequency = 0.0015f;
        public float plateauFrequency = 0.0015f;
        public float hillThreshold = 0.552f;
        public int plateauSharpness = 4;
        public float undulationAmplitude = 0.02f;
        public float baseHeight = 0.03f;
        public float amplitude = 0.2f;
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
