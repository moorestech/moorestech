using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // メサバイオームの高さ生成パラメータと配置サブ設定（実行時 POCO）。
    // Mesa biome height parameters and placement sub-configs (runtime POCO).
    public class MesaBiomeConfig
    {
        public float elevationThreshold = 0.42f;
        public float humidityThreshold = 0.38f;
        public float warpStrength = 140f;
        public int warpIterations = 1;
        public float frequency = 0.002f;
        public int octaves = 1;
        public float persistence = 0.5f;
        public float isolationFreqMult = 2f;
        public float canyonDepth = 0.297f;
        public float canyonFreqMult = 0.1f;
        public int canyonOctaves = 3;
        public float boundaryNoiseStrength = 0.3f;
        public float boundaryNoiseFreqMult = 4f;
        public int boundaryNoiseOctaves = 4;
        public float butteThreshold = 0.321f;
        public float cliffSteepness = 7f;
        public float plateauFlatten = 0f;
        public int terraceSteps = 3;
        public float terraceSharpness = 0.99f;
        public float floorVariation = 0.102f;
        public float topNoiseStrength = 0.051f;
        public float topNoiseFreqMult = 5f;
        public float baseHeight = 0.05f;
        public float amplitude = 0.1f;
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
