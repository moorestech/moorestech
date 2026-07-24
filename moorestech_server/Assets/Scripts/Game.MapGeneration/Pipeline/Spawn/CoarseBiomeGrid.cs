namespace Game.MapGeneration.Pipeline.Spawn
{
    // 段1の粗グリッド分類結果。biomeIndex は有効バイオーム配列へのインデックス、海は-1。
    // Stage-1 coarse grid classification; biomeIndex indexes the active biome array, -1 = sea.
    public sealed class CoarseBiomeGrid
    {
        public readonly int[] BiomeIndex;
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        public readonly float OriginX;
        public readonly float OriginZ;

        public CoarseBiomeGrid(int[] biomeIndex, int width, int height,
            float cellSize, float originX, float originZ)
        {
            BiomeIndex = biomeIndex;
            Width = width;
            Height = height;
            CellSize = cellSize;
            OriginX = originX;
            OriginZ = originZ;
        }
    }
}
