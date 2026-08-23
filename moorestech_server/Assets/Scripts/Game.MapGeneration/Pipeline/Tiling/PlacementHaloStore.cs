using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using UnityEngine;

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
        private readonly List<PlacedVein> _confirmedVeins = new();

        // 全制約のうち最大の距離。これを超える点はどの判定にも効かないので取り込まない。
        // The largest distance any constraint asks for; points beyond it affect no test and are never taken in.
        public readonly float Radius;

        public PlacementHaloStore(float radius)
        {
            Radius = radius;
        }

        // 先行タイルと先行種別の確定AABBを後着鉱脈の排他入力にする。
        // Snapshots confirmed AABBs from earlier tiles and kinds for later-vein exclusion.
        internal List<PlacedVein> CreateConfirmedVeinSnapshot()
        {
            return new List<PlacedVein>(_confirmedVeins);
        }

        internal void CommitItemVeins(ConfirmedVeinPlacementBatch placement)
        {
            CommitVeins(placement, ItemVeinMembers, ItemVeinCenters);
        }

        internal void CommitFluidVeins(ConfirmedVeinPlacementBatch placement)
        {
            CommitVeins(placement, FluidVeinMembers, FluidVeinCenters);
        }

        private void CommitVeins(
            ConfirmedVeinPlacementBatch placement,
            PlacementHaloChannel memberHalo,
            PlacementHaloChannelMap centerHalos)
        {
            // AABBと距離判定用点を同じ確定境界で一括commitする。
            // Commits AABBs and distance-test points at the same confirmation boundary.
            _confirmedVeins.AddRange(placement.Veins);
            foreach (var cluster in placement.Clusters)
            {
                centerHalos.Get(cluster.VeinGuid).Add(cluster.WorldCenter.x, cluster.WorldCenter.y);
                memberHalo.AddPlacements(cluster.Members, 0f, 0f);
            }
        }
    }

    internal sealed class ConfirmedVeinPlacementBatch
    {
        public readonly List<PlacedVein> Veins = new();
        public readonly List<ConfirmedVeinCluster> Clusters = new();
    }

    internal sealed class ConfirmedVeinCluster
    {
        public readonly string VeinGuid;
        public readonly Vector2 WorldCenter;
        public readonly List<PlacementEntry> Members = new();

        public ConfirmedVeinCluster(string veinGuid, Vector2 worldCenter)
        {
            VeinGuid = veinGuid;
            WorldCenter = worldCenter;
        }
    }
}
