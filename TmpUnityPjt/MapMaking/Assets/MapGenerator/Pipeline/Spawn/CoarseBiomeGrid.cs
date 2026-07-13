namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// 段1の粗グリッド分類結果。biomeIndex は有効バイオーム配列(biomeTypes[])へのインデックス、海は-1。
    /// セル中心のワールド座標との相互変換を提供する。
    /// </summary>
    public sealed class CoarseBiomeGrid
    {
        public readonly int[] BiomeIndex; // length = Width*Height, -1=sea
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;   // m
        public readonly float OriginX;    // ワールド座標 m（セル(0,0)中心）
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
