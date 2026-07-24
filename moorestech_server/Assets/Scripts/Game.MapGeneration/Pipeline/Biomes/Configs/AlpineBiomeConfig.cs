using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // 高山バイオームの高さ生成・台地検出パラメータと配置サブ設定（実行時 POCO）。
    // Alpine biome height/plateau parameters and placement sub-configs (runtime POCO).
    public class AlpineBiomeConfig
    {
        public float elevationThreshold = 0.58f;
        public float warpStrength = 4f;
        public int warpIterations = 3;
        public float frequency = 0.001f;
        public int octaves = 3;
        public float ridgeBlend = 0.64f;
        public int ridgeOctaves = 5;
        public float exponent = 1.72f;
        public float ceilHeight = 0.682f;
        public float ceilStrength = 0.858f;
        public float floorHeight = 0.58f;
        public float floorStrength = 0.4f;
        public float baseHeight = 0.06f;
        public float amplitude = 0.6f;
        public bool enablePlateau = true;
        public float prominenceThreshold = 0.06f;
        public int minProminentDirections = 6;
        public int plateauSearchBaseRadius = 8;
        public float plateauBoundaryBlend = 0.6f;
        public int minRegionSize = 390;
        public float minPlateauCoverage = 0.6f;
        public float coverageTolerance = 0.01f;
        public float plateauBaseTransition = 3f;
        public float plateauTransitionScale = 300f;
        public int smoothRadius = 4;
        public int smoothIterations = 4;
        public int boundaryInnerBand = 3;
        public int boundaryOuterBand = 4;
        public float boundaryGaussSigma = 1.8f;
        public float boundaryNoiseFrequency = 0.08f;
        public float boundaryNoiseAmplitude = 0.004f;
        public int boundaryNoiseOctaves = 2;
        public int boundaryRefineIterations = 2;
        public bool debugPlateauOverlay = true;
        public string[] debugTerrainLayerAddressablePaths = new string[0];
        public string terrainLayerAddressablePath = "";
        public TreePlacementConfig treePlacement = new TreePlacementConfig();
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();
    }
}
