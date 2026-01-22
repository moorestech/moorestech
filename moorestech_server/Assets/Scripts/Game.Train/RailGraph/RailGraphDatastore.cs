using Game.Train.RailGraph.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public class RailGraphDatastore : IRailGraphDatastore
    {
        private readonly IReadOnlyList<IRailGraphNodeRemovalListener> _nodeRemovalListeners;
        private Dictionary<RailNode, int> railNodeToId;
        private List<RailNode> railNodes;
        private RailNodeIdAllocator nodeIdAllocator;
        private List<List<(int, int)>> connectNodes;
        private RailGraphPathFinder _pathFinder;
        private Dictionary<ConnectionDestination, int> connectionDestinationToRailId;
        private Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> railPositionToConnectionDestination;

        // レールグラフ更新イベント
        // Rail graph update events
        private readonly RailNodeInitializationNotifier _nodeInitializationNotifier;
        private readonly RailConnectionInitializationNotifier _connectionInitializationNotifier;
        private readonly RailNodeRemovalNotifier _nodeRemovalNotifier;
        private readonly RailConnectionRemovalNotifier _connectionRemovalNotifier;

        public IObservable<RailNodeInitializationData> GetRailNodeInitializedEvent() => _nodeInitializationNotifier.RailNodeInitializedEvent;
        public IObservable<RailConnectionInitializationData> GetRailConnectionInitializedEvent() => _connectionInitializationNotifier.RailConnectionInitializedEvent;
        public IObservable<RailNodeRemovedData> GetRailNodeRemovedEvent() => _nodeRemovalNotifier.RailNodeRemovedEvent;
        public IObservable<RailConnectionRemovalData> GetRailConnectionRemovedEvent() => _connectionRemovalNotifier.RailConnectionRemovedEvent;

        // ハッシュキャッシュ制御
        // Hash cache control
        private uint _cachedGraphHash;
        private bool _isHashDirty;

        // 依存サービスを受け取り、レールグラフの状態を初期化する
        // Initialize the rail graph with required services
        public RailGraphDatastore(IEnumerable<IRailGraphNodeRemovalListener> nodeRemovalListeners)
        {
            _nodeRemovalListeners = nodeRemovalListeners.ToList();
            InitializeDataStore();
            // RailNode -> RailComponentID の解決ロジックを Notifier に渡す
            // Pass node resolution hooks to notifiers
            _nodeInitializationNotifier = new RailNodeInitializationNotifier(this);
            _connectionInitializationNotifier = new RailConnectionInitializationNotifier(this);
            _nodeRemovalNotifier = new RailNodeRemovalNotifier();
            _connectionRemovalNotifier = new RailConnectionRemovalNotifier();
        }

        private void InitializeDataStore()
        {
            railNodeToId = new Dictionary<RailNode, int>();
            railNodes = new List<RailNode>();
            nodeIdAllocator = new RailNodeIdAllocator(EnsureRailNodeSlot);
            connectNodes = new List<List<(int, int)>>();
            connectionDestinationToRailId = new Dictionary<ConnectionDestination, int>();
            railPositionToConnectionDestination = new Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)>();
            _pathFinder = new RailGraphPathFinder();
            _cachedGraphHash = 0;
            _isHashDirty = true;
        }

        private void ResetInternalState()
        {
            // railNodes上のノードを全削除する
            // Remove all rail nodes from the datastore
            foreach (var node in railNodes.ToList())
            {
                if (node != null)
                    RemoveNode(node);
            }
            // RailGraphUpdateEvent の再生成を Notifier に委譲
            // Reset notifier streams for a clean state
            _nodeInitializationNotifier.Reset();
            _connectionInitializationNotifier.Reset();
            _nodeRemovalNotifier.Reset();
            _connectionRemovalNotifier.Reset();
            InitializeDataStore();
        }

        public void Reset()
        {
            ResetInternalState();
        }

        //======================================================
        // 外部から利用するレールグラフAPI
        // Public rail graph API surface
        //======================================================

        public void AddNodeSingle(RailNode node) => AddNodeSingleInternal(node);
        public void AddNodePair(RailNode node1, RailNode node2) => AddNodePairInternal(node1, node2);
        public void ConnectNode(RailNode node, RailNode targetNode, int distance) => ConnectNodeInternal(node, targetNode, distance);
        public void DisconnectNode(RailNode node, RailNode targetNode) => DisconnectNodeInternal(node, targetNode);
        public List<(IRailNode, int)> GetConnectedNodesWithDistance(IRailNode node) => GetConnectedNodesWithDistanceInternal(node);
        public void RemoveNode(RailNode node) => RemoveNodeInternal(node);
        public List<IRailNode> FindShortestPath(IRailNode start, IRailNode target) => FindShortestPathInternal(start.NodeId, target.NodeId);
        public int GetDistanceBetweenNodes(int startid, int targetid) => GetDistanceBetweenNodesInternal(startid, targetid);
        public int GetDistanceBetweenNodes(IRailNode start, IRailNode target) => GetDistanceBetweenNodesInternal(start.NodeId, target.NodeId);
        public List<IRailNode> FindShortestPath(int startid, int targetid) => FindShortestPathInternal(startid, targetid);
        public bool TryGetRailNodeId(RailNode node, out int nodeId) => TryGetRailNodeIdInternal(node, out nodeId);
        public bool TryGetRailNode(int nodeId, out RailNode railNode) => TryGetRailNodeInternal(nodeId, out railNode);
        public uint GetConnectNodesHash() => GetGraphHashInternal();
        public RailGraphSnapshot CaptureSnapshot(long currentTick) => CaptureSnapshotInternal(currentTick);
        public IReadOnlyList<RailNode> GetRailNodes() => railNodes;
        public Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> GetRailPositionToConnectionDestination() => railPositionToConnectionDestination;

        //======================================================
        //  実装 (インスタンスメソッド)
        //======================================================
        private void EnsureRailNodeSlot(int nodeId)
        {
            while (railNodes.Count <= nodeId)
            {
                railNodes.Add(null);
                connectNodes.Add(new List<(int, int)>());
            }
        }

        private void AddNodeSingleInternal(RailNode node)
        {
            if (railNodeToId.ContainsKey(node))
                return;
            var nodeId = nodeIdAllocator.Rent();
            connectNodes[nodeId].Clear();
            railNodes[nodeId] = node;
            railNodeToId[node] = nodeId;
            if (!node.ConnectionDestination.IsDefault())
                connectionDestinationToRailId[node.ConnectionDestination] = nodeId;
            _nodeInitializationNotifier.Notify(nodeId);
            MarkHashDirty();
        }

        private void AddNodePairInternal(RailNode node1, RailNode node2)
        {
            if (railNodeToId.ContainsKey(node1))
                return;
            if (railNodeToId.ContainsKey(node2))
                return;
            var (nodeId1, nodeId2) = nodeIdAllocator.Rent2();
            connectNodes[nodeId1].Clear();
            connectNodes[nodeId2].Clear();
            railNodes[nodeId1] = node1;
            railNodes[nodeId2] = node2;
            railNodeToId[node1] = nodeId1;
            railNodeToId[node2] = nodeId2;

            if (!node1.ConnectionDestination.IsDefault())
                connectionDestinationToRailId[node1.ConnectionDestination] = nodeId1;
            if (!node2.ConnectionDestination.IsDefault())
                connectionDestinationToRailId[node2.ConnectionDestination] = nodeId2;

            _nodeInitializationNotifier.Notify(nodeId1);
            _nodeInitializationNotifier.Notify(nodeId2);
            MarkHashDirty();
        }

        private void ConnectNodeInternal(RailNode node, RailNode targetNode, int distance)
        {
            if (!railNodeToId.ContainsKey(node))
                throw new InvalidOperationException("Attempted to connect a RailNode that is not registered in RailGraphDatastore.");
            var nodeid = railNodeToId[node];
            if (!railNodeToId.ContainsKey(targetNode))
                throw new InvalidOperationException("Attempted to connect to a RailNode that is not registered in RailGraphDatastore.");
            var targetid = railNodeToId[targetNode];
            if (!connectNodes[nodeid].Any(x => x.Item1 == targetid))
            {
                connectNodes[nodeid].Add((targetid, distance));
            }
            else // すでに接続が存在する場合は距離を上書き
            {
                connectNodes[nodeid].RemoveAll(x => x.Item1 == targetid);
                connectNodes[nodeid].Add((targetid, distance));
            }

            // レールグラフ更新イベントを発行
            // Fire rail graph update event
            // 距離更新の場合は上書き
            // In case of distance update, overwrite
            _connectionInitializationNotifier.Notify(nodeid, targetid, distance);

            MarkHashDirty();
        }

        private void DisconnectNodeInternal(RailNode node, RailNode targetNode)
        {
            var nodeid = railNodeToId[node];
            var targetid = railNodeToId[targetNode];
            connectNodes[nodeid].RemoveAll(x => x.Item1 == targetid);
            // 接続解除イベントを発行
            // Broadcast the connection removal
            _connectionRemovalNotifier.Notify(nodeid, node.Guid, targetid, targetNode.Guid);
            MarkHashDirty();
        }

        private void RemoveNodeInternal(RailNode node)
        {
            if (!railNodeToId.ContainsKey(node))
                return;
            foreach (var listener in _nodeRemovalListeners)
            {
                listener.NotifyNodeRemoval(node);
            }
            var nodeid = railNodeToId[node];

            // ノード削除差分をクライアントへ通知
            // Emit node removal diff for clients
            _nodeRemovalNotifier.Notify(nodeid, node.Guid);

            railNodeToId.Remove(node);
            if (node.HasConnectionDestination)
            {
                connectionDestinationToRailId.Remove(node.ConnectionDestination);
            }
            railNodes[nodeid] = null;
            nodeIdAllocator.Return(nodeid);
            connectNodes[nodeid].Clear();
            RemoveNodeTo(nodeid);
            MarkHashDirty();
        }

        private void RemoveNodeTo(int nodeid)
        {
            for (int i = 0; i < connectNodes.Count; i++)
            {
                connectNodes[i].RemoveAll(x => x.Item1 == nodeid);
            }
            MarkHashDirty();
        }

        private bool TryGetRailNodeIdInternal(RailNode node, out int nodeId)
        {
            nodeId = -1;
            if (node == null)
            {
                return false;
            }
            return railNodeToId.TryGetValue(node, out nodeId);
        }

        private bool TryGetRailNodeInternal(int nodeId, out RailNode node)
        {
            node = null;
            if (nodeId < 0 || nodeId >= railNodes.Count)
            {
                return false;
            }
            node = railNodes[nodeId];
            return node != null;
        }

        private RailNode ResolveRailNodeInternal(ConnectionDestination destination)
        {
            if (destination.IsDefault())
            {
                return null;
            }
            if (!connectionDestinationToRailId.TryGetValue(destination, out var nodeId))
            {
                return null;
            }
            if ((nodeId < 0) || (nodeId >= railNodes.Count))
            {
                return null;
            }
            return railNodes[nodeId];
        }

        private List<(IRailNode, int)> GetConnectedNodesWithDistanceInternal(IRailNode node)
        {
            if (node == null)
                return new List<(IRailNode, int)>();
            int nodeId = node.NodeId; // railNodeToId[node];
            if (nodeId < 0 || nodeId >= railNodes.Count)
                return new List<(IRailNode, int)>();
            return connectNodes[nodeId].Select(x => (railNodes[x.Item1] as IRailNode, x.Item2)).ToList();
        }

        private int GetDistanceBetweenNodesInternal(int startid, int targetid)
        {
            if (!TryGetRailNodeInternal(startid, out var start))
                return -1;
            if (!TryGetRailNodeInternal(targetid, out var target))
                return -1;

            foreach (var (neighbor, distance) in connectNodes[startid])
            {
                if (neighbor == targetid)
                    return distance;
            }
            Debug.LogWarning("RailNodeが見つかりません " + startid + " to " + targetid);
            return -1;
        }

        // ダイクストラでstartからtargetへの最短経路を探索
        private List<IRailNode> FindShortestPathInternal(int startid, int targetid)
        {
            // RailGraphPathFinder に探索処理を委譲
            // ID 列を RailNode 列へ変換
            var pathIds = _pathFinder.FindShortestPath(connectNodes, startid, targetid);
            var result = new List<IRailNode>(pathIds.Count);
            for (int i = 0; i < pathIds.Count; i++)
            {
                int id = pathIds[i];
                if (id >= 0 && id < railNodes.Count)
                {
                    result.Add(railNodes[id]);
                }
                else
                {
                    // 想定外のIDが返った場合はnullを入れる（後段で扱えるようにする）
                    result.Add(null);
                }
            }
            return result;
        }

        private uint GetGraphHashInternal()
        {
            if (!_isHashDirty)
                return _cachedGraphHash;
            _cachedGraphHash = RailGraphHashCalculator.ComputeGraphStateHash(railNodes, connectNodes);
            _isHashDirty = false;
            return _cachedGraphHash;
        }

        private void MarkHashDirty()
        {
            _isHashDirty = true;
        }

        private RailGraphSnapshot CaptureSnapshotInternal(long currentTick)
        {
            var nodes = new List<RailNodeInitializationData>(railNodes.Count);
            for (var i = 0; i < railNodes.Count; i++)
            {
                if (railNodes[i] == null)
                    continue;
                nodes.Add(
                    new RailNodeInitializationData(
                        i,
                        railNodes[i].Guid,
                        railNodes[i].ConnectionDestination,
                        railNodes[i].FrontControlPoint.OriginalPosition,
                        railNodes[i].FrontControlPoint.ControlPointPosition,
                        railNodes[i].BackControlPoint.ControlPointPosition)
                    );
            }

            var connections = new List<RailGraphConnectionSnapshot>();
            for (var fromId = 0; fromId < connectNodes.Count; fromId++)
            {
                foreach (var (targetId, distance) in connectNodes[fromId])
                {
                    connections.Add(new RailGraphConnectionSnapshot(fromId, targetId, distance));
                }
            }

            var hash = GetGraphHashInternal();
            return new RailGraphSnapshot(nodes, connections, hash, currentTick);
        }

        // ------------interface実装------------
        public IRailNode ResolveRailNode(ConnectionDestination destination)
        {
            return ResolveRailNodeInternal(destination);
        }

        IReadOnlyList<IRailNode> IRailGraphProvider.FindShortestPath(IRailNode start, IRailNode end)
        {
            if (start == null || end == null)
                return Array.Empty<IRailNode>();
            return FindShortestPathInternal(start.NodeId, end.NodeId);
        }

        public int GetDistance(IRailNode start, IRailNode end, bool useFindPath)
        {
            if (start == null || end == null)
                return -1;
            if (!useFindPath)
                return GetDistanceBetweenNodesInternal(start.NodeId, end.NodeId);

            var path = FindShortestPathInternal(start.NodeId, end.NodeId);
            return CalculatePathDistance(path);
        }
        // ------------interface実装ここまで------------

        private static int CalculatePathDistance(IReadOnlyList<IRailNode> nodes)
        {
            if (nodes == null || nodes.Count < 2)
                return -1;
            int totalDistance = 0;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                var segmentDistance = nodes[i].GetDistanceToNode(nodes[i + 1]);
                if (segmentDistance <= 0)
                    return -1;
                totalDistance += segmentDistance;
            }
            return totalDistance;
        }
    }
}
