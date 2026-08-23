namespace Game.MapGeneration.Pipeline.Tiling
{
    // 格子1つぶんの halo 帳面。確定済みタイルの配置を種類ごとに溜め、以降のタイルの近傍判定へ供給する。
    // 鉱脈はメンバーと中心を分け、中心はさらにveinGuid別に持つ。中心排他はエントリ内にのみ効かせるため。
    // One grid's halo ledgers, holding confirmed placements per kind and feeding later tiles' neighbour tests.
    // Veins split members from centers, and centers further split per veinGuid so center exclusion stays within an entry.
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
