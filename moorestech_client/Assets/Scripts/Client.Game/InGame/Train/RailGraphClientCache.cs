using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using UnityEngine;
using RailNodeInitData = Game.Train.RailGraph.RailNodeInitializationNotifier.RailNodeInitializationData;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailGraphの差分同期結果を保持するクライアント側キャッシュ
    ///     Client-side cache that mirrors the rail graph data for diff-based sync
    /// </summary>
    public sealed class RailGraphClientCache
    {
        // RailNodeInitializationDataのスナップショットを保持
        // Stores RailNodeInitializationData snapshots per nodeId
        private readonly List<RailNodeInitData> _nodeStates = new();

        // RailGraphDatastoreと同型の接続リスト（indexがRailNodeId）
        // Connection list equivalent to RailGraphDatastore (index equals RailNodeId)
        private readonly List<List<(int targetId, int distance)>> _connectNodes = new();
        // キャッシュをIRailNode化したリスト（indexがRailNodeId）
        // IRailNode-compatible view of cached nodes aligned by RailNodeId
        private readonly List<ClientRailNode> _clientNodes = new();
        private readonly RailGraphIdPathFinder _idPathFinder = new();

        private static readonly Vector3 DefaultPosition = new Vector3(-1f, -1f, -1f);

        // ConnectionDestination→RailNodeIdの逆引き辞書
        // Reverse lookup dictionary from ConnectionDestination to RailNodeId
        private readonly Dictionary<ConnectionDestination, int> _connectionDestinationToNodeId = new();

        // 差分適用済みの最新Tick
        // Latest tick that has been fully applied to the cache
        private long _lastConfirmedTick;

        // RailNodeスナップショットを公開（読み取りのみ）
        // Expose rail node snapshots as read-only
        public IReadOnlyList<RailNodeInitData> NodeStates => _nodeStates;

        // 接続情報の参照を外部へ公開（読み取りのみ）
        // Expose connection adjacency list as read-only
        public IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> ConnectNodes => _connectNodes;

        // ConnectionDestination→RailNodeIdの逆引き（読み取りのみ）
        // Expose reverse lookup dictionary as read-only
        public IReadOnlyDictionary<ConnectionDestination, int> ConnectionDestinationIndex => _connectionDestinationToNodeId;

        // IRailNode向けのキャッシュビュー（読み取りのみ）
        // Expose IRailNode-backed cache view
        public IReadOnlyList<ClientRailNode> ClientNodes => _clientNodes;
        public event Action<IReadOnlyList<ClientRailNode>> ClientNodesRebuilt;

        // 最新Tickを確認するためのプロパティ
        // Property to observe the newest applied tick
        public long LastConfirmedTick => _lastConfirmedTick;

        public uint ComputeCurrentHash()
        {
            return RailGraphHashCalculator.ComputeGraphStateHash(_nodeStates, _connectNodes);
        }

        internal void OverrideTick(long serverTick)
        {
            _lastConfirmedTick = Math.Max(_lastConfirmedTick, serverTick);
        }

        // スナップショットを受け取ってキャッシュ全体を再構築する
        // Rebuild the cache from a full snapshot
        public void ApplySnapshot(
            IReadOnlyList<RailNodeInitData> snapshotNodes,
            IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> snapshotConnectNodes,
            long snapshotTick)
        {
            // 入力整合性:skip してからクリア＆コピー
            // Validate inputs(:skip) before clearing the cache and copying data
            ResetSlots(snapshotNodes.Count);
            CopySnapshotData();
            _lastConfirmedTick = snapshotTick;
            RebuildClientNodes();
            TrainRailObjectManager.Instance?.OnCacheRebuilt(this);

            #region Internal

            void ResetSlots(int requiredCount)
            {
                _nodeStates.Clear();
                _connectNodes.Clear();
                _connectionDestinationToNodeId.Clear();
                for (var i = 0; i < requiredCount; i++)
                {
                    _nodeStates.Add(CreateDefaultNode(i));
                    _connectNodes.Add(new List<(int targetId, int distance)>());
                }
            }

            void CopySnapshotData()
            {
                for (var i = 0; i < snapshotNodes.Count; i++)
                {
                    StoreNodeState(i, snapshotNodes[i]);
                    var destination = _connectNodes[i];
                    destination.Clear();
                    var sources = snapshotConnectNodes[i];
                    for (var j = 0; j < sources.Count; j++)
                    {
                        destination.Add(sources[j]);
                    }
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
            EnsureNodeSlot(nodeId);
            var state = new RailNodeInitData(nodeId, nodeGuid, connectionDestination, controlOrigin, primaryControlPoint, oppositeControlPoint);
            StoreNodeState(nodeId, state);
            UpdateTick(eventTick);
            RebuildClientNodes();
        }

        // ノード削除差分を適用し関連接続も破棄する
        // Apply node removal diff and purge related connections
        public void RemoveNode(int nodeId, long eventTick)
        {
            if (!IsWithinCurrentRange(nodeId))
            {
                return;
            }

            StoreNodeState(nodeId, CreateDefaultNode(nodeId));
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
            RebuildClientNodes();
        }

        // 接続情報の差分を適用する（存在すれば距離上書き）
        // Apply or overwrite an edge diff connecting two nodes
        public void UpsertConnection(int fromNodeId, int toNodeId, int distance, long eventTick)
        {
            if (!IsActiveNode(fromNodeId) || !IsActiveNode(toNodeId))
            {
                return;
            }

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
            RebuildClientNodes();
            TrainRailObjectManager.Instance?.OnConnectionUpserted(fromNodeId, toNodeId, this);
        }

        // 接続削除の差分を適用する
        // Apply an edge removal diff
        public void RemoveConnection(int fromNodeId, int toNodeId, long eventTick)
        {
            if (!IsWithinCurrentRange(fromNodeId))
            {
                return;
            }

            var removed = _connectNodes[fromNodeId].RemoveAll(x => x.targetId == toNodeId) > 0;
            if (!removed)
            {
                return;
            }

            UpdateTick(eventTick);
            RebuildClientNodes();
            TrainRailObjectManager.Instance?.OnConnectionRemoved(fromNodeId, toNodeId, this);
        }

        // RailNodeIdからGuid/ControlOriginを取り出す
        // Retrieve Guid and control origin by RailNodeId
        public bool TryGetNode(int nodeId, out Guid nodeGuid, out Vector3 controlOrigin)
        {
            nodeGuid = Guid.Empty;
            controlOrigin = DefaultPosition;
            if (!IsWithinCurrentRange(nodeId))
            {
                return false;
            }

            var state = _nodeStates[nodeId];
            nodeGuid = state.NodeGuid;
            if (nodeGuid == Guid.Empty)
            {
                return false;
            }

            controlOrigin = state.OriginPoint;
            return true;
        }

        // RailNodeIdからConnectionDestinationを取得
        // Retrieve ConnectionDestination from RailNodeId
        public bool TryGetConnectionDestination(int nodeId, out ConnectionDestination destination)
        {
            destination = ConnectionDestination.Default;
            if (!IsWithinCurrentRange(nodeId))
            {
                return false;
            }

            destination = _nodeStates[nodeId].ConnectionDestination;
            return !destination.IsDefault();
        }

        // ConnectionDestinationからRailNodeIdを逆引き
        // Reverse lookup RailNodeId from ConnectionDestination
        public bool TryGetNodeId(ConnectionDestination destination, out int nodeId)
        {
            return _connectionDestinationToNodeId.TryGetValue(destination, out nodeId);
        }

        #region Internal

        // 指定idを格納できるようにリストを拡張する
        // Expand backing lists so the id can be stored safely
        private void EnsureNodeSlot(int nodeId)
        {
            while (_nodeStates.Count <= nodeId)
            {
                _nodeStates.Add(CreateDefaultNode(_nodeStates.Count));
                _connectNodes.Add(new List<(int targetId, int distance)>());
            }
        }

        // ノードのIRailNodeビューを再構成する
        // Rebuild IRailNode-friendly views of cached nodes
        private void RebuildClientNodes()
        {
            _clientNodes.Clear();
            for (var i = 0; i < _nodeStates.Count; i++)
            {
                _clientNodes.Add(new ClientRailNode(_nodeStates[i], _connectNodes, _idPathFinder));
            }
            ClientNodesRebuilt?.Invoke(_clientNodes);
        }

        // 有効範囲かどうか簡易チェック
        // Quick check to see if the index is within range
        private bool IsWithinCurrentRange(int nodeId)
        {
            return nodeId >= 0 && nodeId < _nodeStates.Count;
        }

        // 指定ノードがアクティブ（Guid割当済み）か判定
        // Determine if the node slot already has a Guid assigned
        private bool IsActiveNode(int nodeId)
        {
            return IsWithinCurrentRange(nodeId) && _nodeStates[nodeId].NodeGuid != Guid.Empty;
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

        // ConnectionDestinationテーブルと逆引きを同期する
        // Keep destination table and reverse lookup dictionary in sync
        private void StoreNodeState(int nodeId, RailNodeInitData next)
        {
            var previous = _nodeStates[nodeId];
            _nodeStates[nodeId] = next;
            UpdateDestinationMap(nodeId, previous.ConnectionDestination, next.ConnectionDestination);
        }

        private void UpdateDestinationMap(int nodeId, ConnectionDestination previous, ConnectionDestination next)
        {
            if (!previous.IsDefault())
            {
                _connectionDestinationToNodeId.Remove(previous);
            }

            if (!next.IsDefault())
            {
                _connectionDestinationToNodeId[next] = nodeId;
            }
        }

        private static RailNodeInitData CreateDefaultNode(int nodeId)
        {
            return new RailNodeInitData(
                nodeId,
                Guid.Empty,
                ConnectionDestination.Default,
                DefaultPosition,
                DefaultPosition,
                DefaultPosition);
        }

        #endregion
    }
}
