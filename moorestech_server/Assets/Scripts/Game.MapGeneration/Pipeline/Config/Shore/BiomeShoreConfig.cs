namespace Game.MapGeneration.Pipeline.Config
{
    // 海岸線と水際判定の共通設定（beachLayer TerrainLayer は addressablePath に置換）。
    // Common shore/water-edge config (beachLayer TerrainLayer replaced by addressablePath).
    public class BiomeShoreConfig
    {
        public float waterMargin = 0.03f;
        public float beachElevation = 0.0058f;
        public int beachLandTextureRadius = 16;
        public int beachLandTerrainRadius = 10;
        public int beachSeaTextureRadius = 14;
        public int beachSeaTerrainRadius = 11;
        public string beachLayerAddressablePath = "";
        public float beachThreshold = 0.01f;
        public float deepSeaThreshold = 0.005f;
        public float sandBlendThreshold = 0.01f;
        public int rockFallbackLayerIndex = 1;
        public int minSeaRegionSize = 3672;
    }
}
