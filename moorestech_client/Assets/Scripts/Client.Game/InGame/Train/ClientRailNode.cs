using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    // クライアントキャッシュから読み出したノードを共通IFで扱う
    // Client-side node wrapper that conforms to the shared IRailNode contract
    public sealed class ClientRailNode : IRailNode
    {
        private static readonly IReadOnlyList<(int targetId, int distance)> EmptyEdges = Array.Empty<(int, int)>();
        private readonly IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> _connectNodes;
        private readonly RailGraphIdPathFinder _pathFinder;
        private readonly RailNodeInitializationNotifier.RailNodeInitializationData _state;

        public ClientRailNode(
            RailNodeInitializationNotifier.RailNodeInitializationData state,
            IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> connectNodes,
            RailGraphIdPathFinder pathFinder)
        {
            _state = state;
            _connectNodes = connectNodes;
            _pathFinder = pathFinder;
        }

        public int NodeId => _state.NodeId;
        public int OppositeNodeId => NodeId >= 0 ? NodeId ^ 1 : -1;
        public Guid NodeGuid => _state.NodeGuid;
        public ConnectionDestination ConnectionDestination => _state.ConnectionDestination;
        public Vector3 OriginPoint => _state.OriginPoint;
        public bool IsActive => NodeGuid != Guid.Empty;

        public IEnumerable<(int nodeId, int distance)> ConnectedNodesWithDistance
        {
            get
            {
                var edges = NodeId >= 0 && NodeId < _connectNodes.Count ? _connectNodes[NodeId] : null;
                return edges ?? EmptyEdges;
            }
        }

        public int GetDistanceToNode(int nodeId, bool useFindPath)
        {
            // アクティブでないノードは距離未定義とする
            // Inactive nodes report an undefined distance
            if (!IsActive || nodeId < 0 || nodeId >= _connectNodes.Count)
                return -1;

            if (!useFindPath)
            {
                foreach (var (targetId, distance) in ConnectedNodesWithDistance)
                {
                    if (targetId == nodeId)
                        return distance;
                }
                return -1;
            }

            var result = _pathFinder.FindShortestPath(_connectNodes, NodeId, nodeId);
            return result.Distance;
        }
    }
}
