using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 最終確定前の鉱脈クラスターを中心とメンバーの対応付きで持つ。
    // Holds uncommitted vein clusters while preserving the center-to-member relationship.
    public sealed class VeinPlacementBatch
    {
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
