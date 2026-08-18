namespace Game.MapGeneration.Pipeline.Tiling
{
    // 格子1つぶんの halo 帳面。確定済みタイルの配置を種類ごとに溜め、以降のタイルの近傍判定へ供給する。
    // 鉱脈だけメンバーとクラスター中心を分けて持つ。中心の間隔判定にメンバー座標を混ぜると境界の帯だけ過剰に空く。
    // One grid's halo ledgers, holding confirmed placements per kind and feeding later tiles' neighbour tests.
    // Veins alone split members from cluster centers; mixing member coordinates into the center spacing would over-thin the seam band.
    public class PlacementHaloStore
    {
        public readonly PlacementHaloChannel Trees = new PlacementHaloChannel();
        public readonly PlacementHaloChannel Objects = new PlacementHaloChannel();
        public readonly PlacementHaloChannel ItemVeinMembers = new PlacementHaloChannel();
        public readonly PlacementHaloChannel ItemVeinCenters = new PlacementHaloChannel();
        public readonly PlacementHaloChannel FluidVeinMembers = new PlacementHaloChannel();
        public readonly PlacementHaloChannel FluidVeinCenters = new PlacementHaloChannel();

        // 全制約のうち最大の距離。これを超える点はどの判定にも効かないので取り込まない。
        // The largest distance any constraint asks for; points beyond it affect no test and are never taken in.
        public readonly float Radius;

        public PlacementHaloStore(float radius)
        {
            Radius = radius;
        }
    }
}
