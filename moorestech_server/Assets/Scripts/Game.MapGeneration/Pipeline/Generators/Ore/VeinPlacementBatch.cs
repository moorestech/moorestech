using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 1タイル分の確定済み鉱脈。AABBは配置と同時に確定し、中心とメンバーの対応も保つ。
    // One tile's confirmed veins; AABBs are settled at placement time, and the center-to-member relationship is kept.
    public sealed class VeinPlacementBatch
    {
        public readonly List<PlacedVein> Veins = new();
        public readonly List<VeinPlacementCluster> Clusters = new();
    }

    public sealed class VeinPlacementCluster
    {
        public readonly string VeinGuid;
        public readonly Vector2 WorldCenter;
        public readonly List<PlacementEntry> Members = new();

        public VeinPlacementCluster(string veinGuid, Vector2 worldCenter)
        {
            VeinGuid = veinGuid;
            WorldCenter = worldCenter;
        }
    }
}
