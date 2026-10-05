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
                // このブロックに属するノードだけを見る（表裏ノードとも同じブロック座標を持つ）
                // Only nodes owned by this block (front and back nodes share the block position)
                var node = cache.Nodes[nodeId];
                if (node == null || node.ConnectionDestination.IsDefault() || (Vector3Int)node.ConnectionDestination.blockPosition != blockPosition) continue;

                // 出る辺だけで両向きを網羅できる（入る辺は対向ノードの出る辺）
                // Outgoing edges cover both directions (an incoming edge is the opposite node's outgoing one)
                foreach (var (targetId, _) in cache.ConnectNodes[nodeId])
                {
                    var canonical = RailSegmentPairing.SelectCanonicalPair(nodeId, targetId);
                    if (seen.Add(canonical)) edges.Add(canonical);
                }
            }
        }
    }
}
