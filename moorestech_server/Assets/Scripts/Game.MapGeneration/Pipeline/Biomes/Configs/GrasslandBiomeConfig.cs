using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // 草原バイオームの高さ生成パラメータと配置サブ設定（実行時 POCO）。
    // Grassland biome height parameters and placement sub-configs (runtime POCO).
    public class GrasslandBiomeConfig
    {
        public float frequency = 0.0004f;
        public float amplitude = 1f;
        public float detailFrequency = 0.02f;
        public float detailAmplitude = 0.08f;
        public float baseHeight = 0.009333f;
        public float hillAmplitude = 0.12f;
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
