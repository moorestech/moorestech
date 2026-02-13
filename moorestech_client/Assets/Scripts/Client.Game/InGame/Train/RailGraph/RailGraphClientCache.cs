using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Server.Util.MessagePack;
using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Network;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     Client-side cache that mirrors the rail graph data for diff-based sync
    /// </summary>
    public sealed class RailGraphClientCache : IRailGraphProvider, IRailGraphTraversalProvider
    {
        // Stores RailNodeInitializationData snapshots per nodeId
        private readonly List<ClientRailNode> _nodes = new();

        // Connection list equivalent to RailGraphDatastore (index equals RailNodeId)
        private readonly List<List<(int targetId, int distance)>> _connectNodes = new();

        // レールセグメントキャッシュ
        // Rail segment cache
        private readonly Dictionary<(int startId, int endId), RailSegment> _railSegments = new();

        private static readonly Vector3 DefaultPosition = new Vector3(-1f, -1f, -1f);

        // Reverse lookup dictionary from ConnectionDestination to RailNodeId
        private readonly Dictionary<ConnectionDestination, int> _connectionDestinationToNodeId = new();

        // Latest tick that has been fully applied to the cache
        private long _lastConfirmedTick;

        // Expose rail node snapshots as read-only
        public IReadOnlyList<ClientRailNode> Nodes => _nodes;

        // Expose connection adjacency list as read-only
        public IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> ConnectNodes => _connectNodes;

        // Expose reverse lookup dictionary as read-only
        public IReadOnlyDictionary<ConnectionDestination, int> ConnectionDestinationIndex => _connectionDestinationToNodeId;

        // Property to observe the newest applied tick
        public long LastConfirmedTick => _lastConfirmedTick;

        private RailGraphPathFinder _pathFinder;//ダイクストラ法

        private RailGraphClientCache()
        {
            _pathFinder = new RailGraphPathFinder();
        }

        public uint ComputeCurrentHash()
        {
            return RailGraphHashCalculator.ComputeGraphStateHash(_nodes, _connectNodes, ResolveRailTypeGuid, ResolveRailDrawable);
        }

        internal void OverrideTick(long serverTick)
        {
            _lastConfirmedTick = Math.Max(_lastConfirmedTick, serverTick);
        }

        // Rebuild the cache from a full snapshot
        public void ApplySnapshot(
            RailGraphSnapshotMessagePack snapshot,int size)
        {
            // Validate inputs(:skip) before clearing the cache and copying data
            ResetSlots(size);
            PopulateNodes(snapshot, _nodes);
            // Apply edge information onto adjacency list
            PopulateConnections(snapshot, _connectNodes);
            
            _lastConfirmedTick = snapshot.GraphTick;
            TrainRailObjectManager.Instance?.OnCacheRebuilt(this);

            #region Internal

            void ResetSlots(int requiredCount)
            {
                _nodes.Clear();
                _connectNodes.Clear();
                _connectionDestinationToNodeId.Clear();
                _railSegments.Clear();
                for (var i = 0; i < requiredCount; i++)
                {
                    _nodes.Add(null);
                    _connectNodes.Add(new List<(int targetId, int distance)>());
                }
            }

            void PopulateNodes(RailGraphSnapshotMessagePack targetSnapshot, List<ClientRailNode> nodeList)
            {
                // Copy core node data with simple assignments
                foreach (var node in targetSnapshot.Nodes)
                {
                    if (node == null || node.NodeId < 0 || node.NodeId >= _nodes.Count)
                    {
                        continue;
                    }
                    var destination = node.ConnectionDestination?.ToConnectionDestination() ?? ConnectionDestination.Default;
                    var origin = node.OriginPoint?.ToUnityVector() ?? DefaultPosition;
                    var primary = node.FrontControlPoint?.ToUnityVector() ?? DefaultPosition;
                    var opposite = node.BackControlPoint?.ToUnityVector() ?? DefaultPosition;
                    _nodes[node.NodeId] = new ClientRailNode(node.NodeId, node.NodeGuid, destination, origin, primary, opposite, this);
                    _connectionDestinationToNodeId[destination] = node.NodeId;
                }
            }

            void PopulateConnections(RailGraphSnapshotMessagePack targetSnapshot, List<List<(int targetId, int distance)>> adjacencyList)
            {
                // Exit early when there are no edges
                if (targetSnapshot.Connections == null)
                {
                    return;
                }
                // 隣接リストへ追加
                // Append adjacency info to each origin node
                foreach (var connection in targetSnapshot.Connections)
                {
                    if (connection == null || connection.FromNodeId < 0 || connection.FromNodeId >= adjacencyList.Count)
                        continue;
                    adjacencyList[connection.FromNodeId].Add((connection.ToNodeId, connection.Distance));
                    UpsertRailSegment(connection.FromNodeId, connection.ToNodeId, connection.Distance, connection.RailTypeGuid, connection.IsDrawable);
                }
            }
            #endregion
        }

        // Apply incremental update for a single node
        public void UpsertNode(
            int nodeId,
            Guid nodeGuid,
            Vector3 controlOrigin,
            ConnectionDestination connectionDestination,
            Vector3 primaryControlPoint,
            Vector3 oppositeControlPoint,
            long eventTick)
        {
            Debug.Log($"UpsertNode: nodeId={nodeId}, connectionDestination={connectionDestination}");
            EnsureNodeSlot(nodeId);
            var previous = _nodes[nodeId];
            if (previous != null && previous.NodeGuid.Equals(nodeGuid))
                return;
            var nextnode = new ClientRailNode(nodeId, nodeGuid, connectionDestination, controlOrigin, primaryControlPoint, oppositeControlPoint, this);
            if (previous != null)
            {
                RemoveNode(nodeId, eventTick);
            }
            _nodes[nodeId] = nextnode;
            _connectionDestinationToNodeId[nextnode.ConnectionDestination] = nodeId;
            UpdateTick(eventTick);
        }

        // Apply node removal diff and purge related connections
        public void RemoveNode(int nodeId, long eventTick)
        {
            if (!IsWithinCurrentRange(nodeId))
                return;
            Debug.Log($"RemoveNode: nodeId={nodeId}");
            var previous = _nodes[nodeId];
            if (previous == null)
                return;
            _nodes[nodeId] = null;
            //UpdateDestinationMap(nodeId, previous.ConnectionDestination, next.ConnectionDestination);
            if (!previous.ConnectionDestination.IsDefault())
                _connectionDestinationToNodeId.Remove(previous.ConnectionDestination);


            var outgoing = _connectNodes[nodeId];
            if (outgoing.Count > 0)
            {
                foreach (var (targetId, _) in outgoing)
                {
                    TrainRailObjectManager.Instance?.OnConnectionRemoved(nodeId, targetId, this);
                    RemoveRailSegment(nodeId, targetId);
                }
                outgoing.Clear();
            }
            RemoveIncomingConnections(nodeId);
            UpdateTick(eventTick);
        }

        // Apply or overwrite an edge diff connecting two nodes
        public void UpsertConnection(int fromNodeId, int toNodeId, int distance, Guid railTypeGuid, bool isDrawable, long eventTick)
        {
            if (!IsWithinCurrentRange(fromNodeId) || !IsWithinCurrentRange(toNodeId))
                return;
            Debug.Log($"UpsertConnection: fromNodeId={fromNodeId}, toNodeId={toNodeId}, distance={distance}");
            var connections = _connectNodes[fromNodeId];
            var replaced = false;
            for (var i = 0; i < connections.Count; i++)
            {
                if (connections[i].targetId != toNodeId) continue;
                connections[i] = (toNodeId, distance);
                replaced = true;
                break;
            }
            if (!replaced)
            {
                connections.Add((toNodeId, distance));
            }
            UpsertRailSegment(fromNodeId, toNodeId, distance, railTypeGuid, isDrawable);
            UpdateTick(eventTick);
            TrainRailObjectManager.Instance?.OnConnectionUpserted(fromNodeId, toNodeId, this);
        }

        // Apply an edge removal diff
        public void RemoveConnection(int fromNodeId, int toNodeId, long eventTick)
        {
            if (!IsWithinCurrentRange(fromNodeId))
                return;
            var removed = _connectNodes[fromNodeId].RemoveAll(x => x.targetId == toNodeId) > 0;
            if (!removed)
                return;
            RemoveRailSegment(fromNodeId, toNodeId);
            UpdateTick(eventTick);
            TrainRailObjectManager.Instance?.OnConnectionRemoved(fromNodeId, toNodeId, this);
        }

        // Retrieve IrailNode by RailNodeId
        public bool TryGetNode(int nodeId, out IRailNode irailnode)
        {
            if (IsWithinCurrentRange(nodeId)) 
            {
                irailnode = _nodes[nodeId];
                return irailnode != null;
            }
            else
            {
                irailnode = null;
                return false;
            }
        }

        // Reverse lookup RailNodeId from ConnectionDestination
        public bool TryGetNodeId(ConnectionDestination destination, out int nodeId)
        {
            return _connectionDestinationToNodeId.TryGetValue(destination, out nodeId);
        }
        public bool TryGetNodeId(IRailNode node, out int nodeId)
        {
            if (node == null)
            {
                nodeId = -1;
                return false;
            }
            var destination = node.ConnectionDestination;
            return _connectionDestinationToNodeId.TryGetValue(destination, out nodeId);
        }

        // Expand backing lists so the id can be stored safely
        private void EnsureNodeSlot(int nodeId)
        {
            while (_nodes.Count <= nodeId)
            {
                _nodes.Add(null);
                _connectNodes.Add(new List<(int targetId, int distance)>());
            }
        }

        // Quick check to see if the index is within range
        private bool IsWithinCurrentRange(int nodeId)
        {
            return nodeId >= 0 && nodeId < _nodes.Count;
        }
        public bool TryValidateEndpoint(int nodeid, Guid guid)
        {
            if (IsWithinCurrentRange(nodeid))
            {
                if (_nodes[nodeid] == null)
                    return false;
                return _nodes[nodeid].NodeGuid.Equals(guid);
            }
            else 
            {
                return false;
            }   
        }

        // レールセグメントを取得する
        // Get a rail segment by key
        public bool TryGetRailSegment(int fromNodeId, int toNodeId, out RailSegment segment)
        {
            return _railSegments.TryGetValue((fromNodeId, toNodeId), out segment);
        }

        // レール種類を取得する
        // Get rail type by segment key
        public bool TryGetRailType(int fromNodeId, int toNodeId, out Guid railTypeGuid)
        {
            if (TryGetRailSegment(fromNodeId, toNodeId, out var segment))
            {
                railTypeGuid = segment.RailTypeGuid;
                return true;
            }
            railTypeGuid = Guid.Empty;
            return false;
        }

        // レール描画可否を取得する
        // Get drawable flag by segment key
        public bool TryGetRailDrawable(int fromNodeId, int toNodeId, out bool isDrawable)
        {
            if (TryGetRailSegment(fromNodeId, toNodeId, out var segment))
            {
                isDrawable = segment.IsDrawable;
                return true;
            }
            isDrawable = false;
            return false;
        }

        // セグメント情報を更新する
        // Upsert segment data into cache
        private void UpsertRailSegment(int fromNodeId, int toNodeId, int length, Guid railTypeGuid, bool isDrawable)
        {
            var key = (fromNodeId, toNodeId);
            if (!_railSegments.TryGetValue(key, out var segment))
            {
                segment = new RailSegment(fromNodeId, toNodeId, length, railTypeGuid);
                _railSegments[key] = segment;
            }
            segment.SetLength(length);
            if (!(railTypeGuid == Guid.Empty && segment.RailTypeGuid != Guid.Empty))
                segment.SetRailType(railTypeGuid);
            segment.SetDrawable(isDrawable);
        }

        // セグメントを削除する
        // Remove a segment from cache
        private void RemoveRailSegment(int fromNodeId, int toNodeId)
        {
            _railSegments.Remove((fromNodeId, toNodeId));
        }

        // レール種類を解決する
        // Resolve rail type guid
        private Guid ResolveRailTypeGuid(int fromNodeId, int toNodeId)
        {
            return TryGetRailType(fromNodeId, toNodeId, out var railTypeGuid) ? railTypeGuid : Guid.Empty;
        }

        // 描画可否を解決する
        // Resolve drawable flag
        private bool ResolveRailDrawable(int fromNodeId, int toNodeId)
        {
            return TryGetRailDrawable(fromNodeId, toNodeId, out var isDrawable) && isDrawable;
        }

        // Remove all incoming edges pointing to the target id
        private void RemoveIncomingConnections(int targetNodeId)
        {
            for (var i = 0; i < _connectNodes.Count; i++)
            {
                var edges = _connectNodes[i];
                if (edges == null || edges.Count == 0)
                {
                    continue;
                }

                for (var index = edges.Count - 1; index >= 0; index--)
                {
                    if (edges[index].targetId != targetNodeId)
                    {
                        continue;
                    }

                    edges.RemoveAt(index);
                    RemoveRailSegment(i, targetNodeId);
                    TrainRailObjectManager.Instance?.OnConnectionRemoved(i, targetNodeId, this);
                }
            }
        }

        // Update the stored tick with the latest value
        private void UpdateTick(long eventTick)
        {
            _lastConfirmedTick = Math.Max(_lastConfirmedTick, eventTick);
        }

        public List<IRailNode> FindShortestPath(int startid, int targetid)
        {
            var pathIds = _pathFinder.FindShortestPath(_connectNodes, startid, targetid);
            var result = new List<IRailNode>(pathIds.Count);
            for (int i = 0; i < pathIds.Count; i++)
            {
                int id = pathIds[i];
                if (id >= 0 && id < _nodes.Count)
                {
                    result.Add(_nodes[id]);
                }
                else
                {
                    result.Add(null);
                }
            }
            return result;
        }

        public IRailNode ResolveRailNode(ConnectionDestination destination)
        {
            if (!TryGetNodeId(destination, out var nodeId) || !IsWithinCurrentRange(nodeId))
            {
                return null;
            }
            return _nodes[nodeId];
        }

        public IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end)
        {
            if (!TryGetNodeId(start, out var startId) || !TryGetNodeId(end, out var endId))
            {
                return Array.Empty<IRailNode>();
            }
            return FindShortestPath(startId, endId);
        }

        public int GetDistance(IRailNode start, IRailNode end, bool useFindPath)
        {
            if (!TryGetNodeId(start, out var startId) || !TryGetNodeId(end, out var endId))
            {
                return -1;
            }

            if (!useFindPath)
            {
                if (!IsWithinCurrentRange(startId))
                {
                    return -1;
                }

                var edges = _connectNodes[startId];
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (edge.targetId == endId)
                    {
                        return edge.distance;
                    }
                }
                return -1;
            }

            var path = FindShortestPath(startId, endId);
            return RailNodeCalculate.CalculateTotalDistanceF(path);
        }

#if UNITY_EDITOR
        // エディタテストからのみ使う生成ヘルパー。
        // Factory helper used only by editor-side tests.
        public static RailGraphClientCache CreateForEditorTest()
        {
            return new RailGraphClientCache();
        }
#endif
    }
}












