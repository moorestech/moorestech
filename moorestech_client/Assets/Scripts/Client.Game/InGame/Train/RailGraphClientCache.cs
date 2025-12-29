using Game.Train.RailGraph;
using Server.Util.MessagePack;
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.Image;
using RailNodeInitData = Game.Train.RailGraph.RailNodeInitializationNotifier.RailNodeInitializationData;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailGraphの差分同期結果を保持するクライアント側キャッシュ
    ///     Client-side cache that mirrors the rail graph data for diff-based sync
    /// </summary>
    public sealed class RailGraphClientCache : IRailGraphProvider
    {
        // RailNodeInitializationDataのスナップショットを保持
        // Stores RailNodeInitializationData snapshots per nodeId
        private readonly List<ClientRailNode> _nodes = new();

        // RailGraphDatastoreと同型の接続リスト（indexがRailNodeId）
        // Connection list equivalent to RailGraphDatastore (index equals RailNodeId)
        private readonly List<List<(int targetId, int distance)>> _connectNodes = new();

        private static readonly Vector3 DefaultPosition = new Vector3(-1f, -1f, -1f);

        // ConnectionDestination→RailNodeIdの逆引き辞書
        // Reverse lookup dictionary from ConnectionDestination to RailNodeId
        private readonly Dictionary<ConnectionDestination, int> _connectionDestinationToNodeId = new();

        // 差分適用済みの最新Tick
        // Latest tick that has been fully applied to the cache
        private long _lastConfirmedTick;

        // RailNodeスナップショットを公開（読み取りのみ）
        // Expose rail node snapshots as read-only
        public IReadOnlyList<ClientRailNode> Nodes => _nodes;

        // 接続情報の参照を外部へ公開（読み取りのみ）
        // Expose connection adjacency list as read-only
        public IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> ConnectNodes => _connectNodes;

        // ConnectionDestination→RailNodeIdの逆引き（読み取りのみ）
        // Expose reverse lookup dictionary as read-only
        public IReadOnlyDictionary<ConnectionDestination, int> ConnectionDestinationIndex => _connectionDestinationToNodeId;

        // 最新Tickを確認するためのプロパティ
        // Property to observe the newest applied tick
        public long LastConfirmedTick => _lastConfirmedTick;

        private RailGraphPathFinder _pathFinder;//ダイクストラ法は専用クラスに委譲する

        private RailGraphClientCache()
        {
            _pathFinder = new RailGraphPathFinder();
            RailGraphProvider.SetProvider(this);
        }

        public uint ComputeCurrentHash()
        {
            return RailGraphHashCalculator.ComputeGraphStateHash(_nodes, _connectNodes);
        }

        internal void OverrideTick(long serverTick)
        {
            _lastConfirmedTick = Math.Max(_lastConfirmedTick, serverTick);
        }

        // スナップショットを受け取ってキャッシュ全体を再構築する
        // Rebuild the cache from a full snapshot
        public void ApplySnapshot(
            RailGraphSnapshotMessagePack snapshot,int size)
        {
            // 入力整合性:skip してからクリア＆コピー
            // Validate inputs(:skip) before clearing the cache and copying data
            ResetSlots(size);
            PopulateNodes(snapshot, _nodes);
            // 辺データを隣接リストへ登録
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
                for (var i = 0; i < requiredCount; i++)
                {
                    _nodes.Add(null);
                    _connectNodes.Add(new List<(int targetId, int distance)>());
                }
            }

            void PopulateNodes(RailGraphSnapshotMessagePack targetSnapshot, List<ClientRailNode> nodeList)
            {
                // ノードごとの必須データを単純代入
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
                // 辺が無ければ終了
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
                }
            }
            #endregion
        }

        // 単体ノードの差分更新を適用する
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
                RemoveNode(nodeId, eventTick);//先にremoveされるので同じnodeidで上書きされることは基本無いが、一応
            }
            _nodes[nodeId] = nextnode;
            _connectionDestinationToNodeId[nextnode.ConnectionDestination] = nodeId;
            UpdateTick(eventTick);
        }

        // ノード削除差分を適用し関連接続も破棄する
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
                }
                outgoing.Clear();
            }
            RemoveIncomingConnections(nodeId);
            UpdateTick(eventTick);
        }

        // 接続情報の差分を適用する（存在すれば距離上書き）
        // Apply or overwrite an edge diff connecting two nodes
        public void UpsertConnection(int fromNodeId, int toNodeId, int distance, long eventTick)
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
            UpdateTick(eventTick);
            TrainRailObjectManager.Instance?.OnConnectionUpserted(fromNodeId, toNodeId, this);
        }

        // 接続削除の差分を適用する
        // Apply an edge removal diff
        public void RemoveConnection(int fromNodeId, int toNodeId, long eventTick)
        {
            if (!IsWithinCurrentRange(fromNodeId))
                return;
            var removed = _connectNodes[fromNodeId].RemoveAll(x => x.targetId == toNodeId) > 0;
            if (!removed)
                return;
            UpdateTick(eventTick);
            TrainRailObjectManager.Instance?.OnConnectionRemoved(fromNodeId, toNodeId, this);
        }

        // RailNodeIdからIrailNodeを取り出す
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

        // ConnectionDestinationからRailNodeIdを逆引き
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

        // 指定idを格納できるようにリストを拡張する
        // Expand backing lists so the id can be stored safely
        private void EnsureNodeSlot(int nodeId)
        {
            while (_nodes.Count <= nodeId)
            {
                _nodes.Add(null);
                _connectNodes.Add(new List<(int targetId, int distance)>());
            }
        }

        // 有効範囲かどうか簡易チェック
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

        // すべてのノードから対象id宛の接続を削除
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
                    TrainRailObjectManager.Instance?.OnConnectionRemoved(i, targetNodeId, this);
                }
            }
        }

        // Tick値を更新（最新値を保持）
        // Update the stored tick with the latest value
        private void UpdateTick(long eventTick)
        {
            _lastConfirmedTick = Math.Max(_lastConfirmedTick, eventTick);
        }

        public List<IRailNode> FindShortestPath(int startid, int targetid)
        {
            // RailGraphPathFinder に処理を委譲
            // ID 列を RailNode 列へ変換
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
                    // 異常系：範囲外なら null を詰めておく（実際には起こらない想定）
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
    }
}
