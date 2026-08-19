namespace Game.MapGeneration.Pipeline.Tiling
{
    // 配置ステージがタイルごとに受け取る、格子の中での自分の居場所と halo 帳面。
    // worldOffset から浮動小数点でタイル index を割り戻すのは不安定なので、index はここを唯一の出所にする。
    // What each placement stage receives per tile: its slot in the grid and the halo ledgers.
    // Dividing worldOffset back into a tile index is unstable, so this is the index's only source.
    public readonly struct TilePlacementContext
    {
        public readonly int TileIndexX;
        public readonly int TileIndexZ;
        public readonly PlacementHaloStore Halo;

        public TilePlacementContext(int tileIndexX, int tileIndexZ, PlacementHaloStore halo)
        {
            TileIndexX = tileIndexX;
            TileIndexZ = tileIndexZ;
            Halo = halo;
        }
    }
}
