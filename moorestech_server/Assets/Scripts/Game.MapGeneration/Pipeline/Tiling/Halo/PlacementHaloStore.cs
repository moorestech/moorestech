using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Generators;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // 格子1つぶんの halo 帳面。確定済みタイルの配置を種類ごとに溜め、以降のタイルの近傍判定へ供給する。
    // 鉱脈のメンバー点がタイル境界の中心間隔判定へ混ざらないよう中心と分け、中心はveinGuid別に持つ。
    // 確定AABB台帳だけは点チャネルとは別の境界で絞る（_confirmedVeins のコメント参照）。
    // One grid's halo ledgers, holding confirmed placements per kind and feeding later tiles' neighbour tests.
    // Vein members stay separate so they never enter cross-tile center-spacing checks; centers are also split per veinGuid.
    // The confirmed-AABB ledger alone uses a different bound than the point channels (see the _confirmedVeins comment).
    public class PlacementHaloStore
    {
        public readonly PlacementHaloChannel Trees = new PlacementHaloChannel();
        public readonly PlacementHaloChannel Objects = new PlacementHaloChannel();
        public readonly VeinHaloChannels ItemVeins =
            new VeinHaloChannels(new PlacementHaloChannel(), new PlacementHaloChannelMap());
        public readonly VeinHaloChannels FluidVeins =
            new VeinHaloChannels(new PlacementHaloChannel(), new PlacementHaloChannelMap());

        // 台帳は Radius でなく到達域判定で絞る。Radius より狭い保証になる。
        // The ledger is filtered by the reachability test rather than Radius, which is the tighter bound.
        private readonly List<PlacedVein> _confirmedVeins = new();

        // 点チャネルの取り込み境界。全制約のうち最大の距離で、超える点はどの判定にも効かない。
        // The intake bound for the point channels: the largest distance any constraint asks for, beyond which a point affects no test.
        public readonly float Radius;

        public PlacementHaloStore(float radius)
        {
            Radius = radius;
        }

        // 先行タイルと先行種別の確定AABBを後着鉱脈の排他入力にする。
        // Snapshots confirmed AABBs from earlier tiles and kinds for later-vein exclusion.
        internal List<PlacedVein> CreateConfirmedVeinSnapshot(
            float tileWorldOffsetX, float tileWorldOffsetZ,
            float tileWidth, float tileLength)
        {
            // 遠隔履歴を候補範囲で除外する。
            // Excludes remote history that cannot touch any candidate AABB in this tile.
            var snapshot = new List<PlacedVein>();
            foreach (var vein in _confirmedVeins)
            {
                if (!VeinAabbBuilder.CanOverlapAnyCandidateInTile(
                        vein, tileWorldOffsetX, tileWorldOffsetZ, tileWidth, tileLength)) continue;
                snapshot.Add(vein);
            }
            return snapshot;
        }

        // 種をまいたチャネル束をそのまま受け取り、commit先の取り違えを起こせなくする。
        // Takes the very channel bundle that was seeded, so committing to the wrong kind cannot happen.
        internal void CommitVeins(VeinHaloChannels channels, VeinPlacementBatch placement)
        {
            // 値型なので台帳は所有コピーを持ち、出力側のscene変換に引きずられない。
            // PlacedVein is a value type, so the ledger owns its copies and output-side scene shifts never reach it.
            _confirmedVeins.AddRange(placement.Veins);

            // AABBと距離判定用点を同じ確定境界で一括commitする。
            // Commits AABBs and distance-test points at the same confirmation boundary.
            foreach (var cluster in placement.Clusters)
            {
                channels.Centers.Get(cluster.VeinGuid).Add(cluster.WorldCenter.x, cluster.WorldCenter.y);
                channels.Members.AddPlacements(cluster.Members, 0f, 0f);
            }
        }
    }
}
