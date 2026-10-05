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
        public static void Collect(RailGraphClientCache cache, Vector3Int blockPosition, ICollection<(int canonicalFrom, int canonicalTo)> edges)
        {
            var seen = new HashSet<(int, int)>();
            for (var nodeId = 0; nodeId < cache.Nodes.Count; nodeId++)
            {
                // このブロックのノードだけを見る
                // Only nodes owned by this block
                var node = cache.Nodes[nodeId];
                if (node == null || node.ConnectionDestination.IsDefault() || (Vector3Int)node.ConnectionDestination.blockPosition != blockPosition) continue;

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
