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
        // 中心haloの帳面を引く鍵。同じveinGuidを撒く2エントリを別チャネルにするため、guidでなく配置元エントリで同定する。
        // The key for the center halo ledger; two entries scattering one veinGuid stay on separate channels, so identity is the source entry rather than the guid.
        public readonly int EntryIndex;

        public readonly string VeinGuid;
        public readonly Vector2 WorldCenter;
        public readonly List<PlacementEntry> Members = new();

        public VeinPlacementCluster(int entryIndex, string veinGuid, Vector2 worldCenter)
        {
            EntryIndex = entryIndex;
            VeinGuid = veinGuid;
            WorldCenter = worldCenter;
        }
    }
}
