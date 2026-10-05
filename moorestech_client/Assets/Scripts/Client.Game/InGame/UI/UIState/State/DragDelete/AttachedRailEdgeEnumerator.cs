using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Game.Train.RailGraph.Utility;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     指定ブロック座標のレールノードから出る区間をcanonical化して列挙する（往復の2辺は1本として数える）
    ///     Enumerates canonical edges leaving rail nodes at a block position (the two directions of one rail count once)
    /// </summary>
    public static class AttachedRailEdgeEnumerator
    {
        public static void Collect(RailGraphClientCache cache, IReadOnlyList<ConnectionDestination> destinations, ICollection<(int canonicalFrom, int canonicalTo)> edges)
        {
            var seen = new HashSet<(int, int)>();
            foreach (var destination in destinations)
            {
                // 同期済みの端点だけから区間を集める
                // Gather edges only from synchronized destinations
                if (!cache.TryGetNodeId(destination, out var nodeId))
                {
                    Debug.LogWarning($"[RemovalCascade] rail node not synced: {destination}");
                    continue;
                }

                // 出る辺だけで両向きを網羅できる
                // Outgoing edges cover both directions
                foreach (var (targetId, _) in cache.ConnectNodes[nodeId])
                {
                    var canonical = RailSegmentPairing.SelectCanonicalPair(nodeId, targetId);
                    if (seen.Add(canonical)) edges.Add(canonical);
                }
            }
        }
    }
}
