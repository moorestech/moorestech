using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // 林バイオームの高さ生成パラメータと配置サブ設定（実行時 POCO）。
    // Woods biome height parameters and placement sub-configs (runtime POCO).
    public class WoodsBiomeConfig
    {
        public float humidityThreshold = 0.4f;
        public float humidityUpperThreshold = 0.52f;
        public float frequency = 0.0012f;
        public int terraceSteps = 5;
        public float terraceSharpness = 0.7f;
        public float baseHeight = 0.05f;
        public float amplitude = 0.15f;
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
