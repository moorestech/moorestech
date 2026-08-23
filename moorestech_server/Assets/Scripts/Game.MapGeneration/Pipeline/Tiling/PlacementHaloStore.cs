namespace Game.MapGeneration.Pipeline.Tiling
{
    // 格子1つぶんの halo 帳面。確定済みタイルの配置を種類ごとに溜め、以降のタイルの近傍判定へ供給する。
    // 鉱脈のメンバー点がタイル境界の中心間隔判定へ混ざらないよう中心と分け、中心はveinGuid別に持つ。
    // One grid's halo ledgers, holding confirmed placements per kind and feeding later tiles' neighbour tests.
    // Vein members stay separate so they never enter cross-tile center-spacing checks; centers are also split per veinGuid.
    public class PlacementHaloStore
    {
        public readonly PlacementHaloChannel Trees = new PlacementHaloChannel();
        public readonly PlacementHaloChannel Objects = new PlacementHaloChannel();
        public readonly PlacementHaloChannel ItemVeinMembers = new PlacementHaloChannel();
        public readonly PlacementHaloChannelMap ItemVeinCenters = new PlacementHaloChannelMap();
        public readonly PlacementHaloChannel FluidVeinMembers = new PlacementHaloChannel();
        public readonly PlacementHaloChannelMap FluidVeinCenters = new PlacementHaloChannelMap();

        // 全制約のうち最大の距離。これを超える点はどの判定にも効かないので取り込まない。
        // The largest distance any constraint asks for; points beyond it affect no test and are never taken in.
        public readonly float Radius;

        public PlacementHaloStore(float radius)
        {
            Radius = radius;
        }
    }
}
