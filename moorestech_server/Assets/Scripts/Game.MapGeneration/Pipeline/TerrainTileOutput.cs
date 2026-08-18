namespace Game.MapGeneration.Pipeline
{
    // 1タイル分の地形出力。Heightsは木摂動前(0-1正規化)。TileX/TileZは転送格子index(0..side-1)
    // Terrain output of one tile; Heights are pre-tree-perturbation (0-1). TileX/TileZ are transfer-grid indices
    public class TerrainTileOutput
    {
        public int TileX;
        public int TileZ;
        public float[] Heights;       // [Resolution*Resolution]
        public byte[] BiomeIndices;   // [Resolution*Resolution]
    }
}
