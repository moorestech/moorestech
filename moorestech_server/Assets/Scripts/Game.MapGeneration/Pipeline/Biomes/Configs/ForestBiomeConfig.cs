using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // 森林バイオームの高さ生成パラメータと配置サブ設定（実行時 POCO）。
    // Forest biome height parameters and placement sub-configs (runtime POCO).
    public class ForestBiomeConfig
    {
        public float humidityThreshold = 0.52f;
        public float warpStrength = 30f;
        public int warpIterations = 2;
        public float baseFrequency = 0.001f;
        public int baseOctaves = 4;
        public float basePersistence = 0.45f;
        public float ridgeBlend = 0f;
        public int ridgeOctaves = 4;
        public float lowlandCutoff = 0.1f;
        public float exponent = 1f;
        public float plateauFlatten = 0f;
        public float detailFrequency = 0.01f;
        public int detailOctaves = 3;
        public float detailWeight = 0.04f;
        public float baseHeight = 0.1f;
        public float amplitude = 0.35f;
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
