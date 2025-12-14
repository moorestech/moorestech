using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailGraphの差分同期結果を保持するクライアント側キャッシュ
    ///     Client-side cache that mirrors the rail graph data for diff-based sync
    /// </summary>
    public sealed class RailGraphClientCache
    {
        // ノードを一意に識別するGuidの連番テーブル
        // Sequential table that stores node Guid per RailNodeId
        private readonly List<Guid> _nodeGuids = new();

        // ControlPositionの起点座標テーブル（indexはRailNodeIdと一致）
        // Table that stores each control position origin aligned to the RailNodeId
        private readonly List<Vector3> _controlPositionOrigins = new();

        // RailGraphDatastoreと同型の接続リスト（indexがRailNodeId）
        // Connection list equivalent to RailGraphDatastore (index equals RailNodeId)
        private readonly List<List<(int targetId, int distance)>> _connectNodes = new();

        // メイン側・反対側のベジェ制御点座標
        // Store primary/opposite control point positions
        private readonly List<Vector3> _primaryControlPoints = new();
        private readonly List<Vector3> _oppositeControlPoints = new();

        // RailNodeIdごとのConnectionDestination
        // ConnectionDestination table per RailNodeId
        private readonly List<ConnectionDestination> _connectionDestinations = new();

        // ConnectionDestination→RailNodeIdの逆引き辞書
        // Reverse lookup dictionary from ConnectionDestination to RailNodeId
        private readonly Dictionary<ConnectionDestination, int> _connectionDestinationToNodeId = new();

        // 差分適用済みの最新Tick
        // Latest tick that has been fully applied to the cache
        private long _lastConfirmedTick;

        // Guid配列の参照を外部へ公開（読み取りのみ）
        // Expose node Guid snapshot as read-only
        public IReadOnlyList<Guid> NodeGuids => _nodeGuids;

        // ControlPosition起点の参照を外部へ公開（読み取りのみ）
        // Expose control position origins as read-only
        public IReadOnlyList<Vector3> ControlPositionOrigins => _controlPositionOrigins;

        // 接続情報の参照を外部へ公開（読み取りのみ）
        // Expose connection adjacency list as read-only
        public IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> ConnectNodes => _connectNodes;

        // ConnectionDestinationの参照を外部へ公開（読み取りのみ）
        // Expose connection destinations per node as read-only
        public IReadOnlyList<ConnectionDestination> ConnectionDestinations => _connectionDestinations;

        // ConnectionDestination→RailNodeIdの逆引き（読み取りのみ）
        // Expose reverse lookup dictionary as read-only
        public IReadOnlyDictionary<ConnectionDestination, int> ConnectionDestinationIndex => _connectionDestinationToNodeId;

        // 制御点座標の参照（読み取りのみ）
        // Expose control point coordinates as read-only
        public IReadOnlyList<Vector3> PrimaryControlPoints => _primaryControlPoints;
        public IReadOnlyList<Vector3> OppositeControlPoints => _oppositeControlPoints;

        // 最新Tickを確認するためのプロパティ
        // Property to observe the newest applied tick
        public long LastConfirmedTick => _lastConfirmedTick;

        // スナップショットを受け取ってキャッシュ全体を再構築する
        // Rebuild the cache from a full snapshot
        public void ApplySnapshot(
            IReadOnlyList<Guid> snapshotNodeGuids,
            IReadOnlyList<Vector3> snapshotControlOrigins,
            IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> snapshotConnectNodes,
            IReadOnlyList<ConnectionDestination> snapshotConnectionDestinations,
            IReadOnlyList<Vector3> snapshotPrimaryControlPoints,
            IReadOnlyList<Vector3> snapshotOppositeControlPoints,
            long snapshotTick)
        {
            // 入力整合性を確認してからクリア＆コピー
            // Validate inputs before clearing the cache and copying data
            ValidateSnapshotInput();
            ResetSlots(snapshotNodeGuids.Count);
            CopySnapshotData();
            _lastConfirmedTick = snapshotTick;

            #region Internal

            void ValidateSnapshotInput()
            {
                if (snapshotNodeGuids == null) throw new ArgumentNullException(nameof(snapshotNodeGuids));
                if (snapshotControlOrigins == null) throw new ArgumentNullException(nameof(snapshotControlOrigins));
                if (snapshotConnectNodes == null) throw new ArgumentNullException(nameof(snapshotConnectNodes));
                if (snapshotNodeGuids.Count != snapshotControlOrigins.Count)
                {
                    throw new ArgumentException("RailNode guid count mismatch with control origins.");
                }
                if (snapshotNodeGuids.Count != snapshotConnectNodes.Count)
                {
                    throw new ArgumentException("RailNode guid count mismatch with connectNodes.");
                }
                if (snapshotNodeGuids.Count != snapshotConnectionDestinations.Count)
                {
                    throw new ArgumentException("RailNode guid count mismatch with connection destinations.");
                }
                if (snapshotNodeGuids.Count != snapshotPrimaryControlPoints.Count)
                {
                    throw new ArgumentException("RailNode guid count mismatch with primary control points.");
                }
                if (snapshotNodeGuids.Count != snapshotOppositeControlPoints.Count)
                {
                    throw new ArgumentException("RailNode guid count mismatch with opposite control points.");
                }
            }

            void ResetSlots(int requiredCount)
            {
                _nodeGuids.Clear();
                _controlPositionOrigins.Clear();
                _connectNodes.Clear();
                _connectionDestinations.Clear();
                _connectionDestinationToNodeId.Clear();
                _primaryControlPoints.Clear();
                _oppositeControlPoints.Clear();
                for (var i = 0; i < requiredCount; i++)
                {
                    _nodeGuids.Add(Guid.Empty);
                    _controlPositionOrigins.Add(new Vector3(-1, -1, -1));
                    _connectNodes.Add(new List<(int targetId, int distance)>());
                    _connectionDestinations.Add(ConnectionDestination.Default);
                    _primaryControlPoints.Add(new Vector3(-1, -1, -1));
                    _oppositeControlPoints.Add(new Vector3(-1, -1, -1));
                }
            }

            void CopySnapshotData()
            {
                for (var i = 0; i < snapshotNodeGuids.Count; i++)
                {
                    _nodeGuids[i] = snapshotNodeGuids[i];
                    _controlPositionOrigins[i] = snapshotControlOrigins[i];
                    _primaryControlPoints[i] = snapshotPrimaryControlPoints[i];
                    _oppositeControlPoints[i] = snapshotOppositeControlPoints[i];
                    AssignConnectionDestination(i, snapshotConnectionDestinations[i]);
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
            _nodeGuids[nodeId] = nodeGuid;
            _controlPositionOrigins[nodeId] = controlOrigin;
            _primaryControlPoints[nodeId] = primaryControlPoint;
            _oppositeControlPoints[nodeId] = oppositeControlPoint;
            AssignConnectionDestination(nodeId, connectionDestination);
            UpdateTick(eventTick);
        }

        // ノード削除差分を適用し関連接続も破棄する
        // Apply node removal diff and purge related connections
        public void RemoveNode(int nodeId, long eventTick)
        {
            if (!IsWithinCurrentRange(nodeId))
            {
                return;
            }

            _nodeGuids[nodeId] = Guid.Empty;
            _controlPositionOrigins[nodeId] = Vector3.zero;
            _connectNodes[nodeId].Clear();
            _primaryControlPoints[nodeId] = Vector3.zero;
            _oppositeControlPoints[nodeId] = Vector3.zero;
            AssignConnectionDestination(nodeId, ConnectionDestination.Default);
            RemoveIncomingConnections(nodeId);
            UpdateTick(eventTick);
        }

        // 接続情報の差分を適用する（存在すれば上書き）
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
        }

        // 接続削除の差分を適用する
        // Apply an edge removal diff
        public void RemoveConnection(int fromNodeId, int toNodeId, long eventTick)
        {
            if (!IsWithinCurrentRange(fromNodeId))
            {
                return;
            }

            _connectNodes[fromNodeId].RemoveAll(x => x.targetId == toNodeId);
            UpdateTick(eventTick);
        }

        // RailNodeIdからGuid/ControlOriginを取り出す
        // Retrieve Guid and control origin by RailNodeId
        public bool TryGetNode(int nodeId, out Guid nodeGuid, out Vector3 controlOrigin)
        {
            nodeGuid = Guid.Empty;
            controlOrigin = Vector3.zero;
            if (!IsWithinCurrentRange(nodeId))
            {
                return false;
            }

            nodeGuid = _nodeGuids[nodeId];
            if (nodeGuid == Guid.Empty)
            {
                return false;
            }

            controlOrigin = _controlPositionOrigins[nodeId];
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

            destination = _connectionDestinations[nodeId];
            return !destination.IsDefault;
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
            while (_nodeGuids.Count <= nodeId)
            {
                _nodeGuids.Add(Guid.Empty);
                _controlPositionOrigins.Add(Vector3.zero);
                _connectNodes.Add(new List<(int targetId, int distance)>());
                _connectionDestinations.Add(ConnectionDestination.Default);
                _primaryControlPoints.Add(Vector3.zero);
                _oppositeControlPoints.Add(Vector3.zero);
            }
        }

        // 有効範囲かどうか簡易チェック
        // Quick check to see if the index is within range
        private bool IsWithinCurrentRange(int nodeId)
        {
            return nodeId >= 0 && nodeId < _nodeGuids.Count;
        }

        // 指定ノードがアクティブ（Guid割当済み）か判定
        // Determine if the node slot already has a Guid assigned
        private bool IsActiveNode(int nodeId)
        {
            return IsWithinCurrentRange(nodeId) && _nodeGuids[nodeId] != Guid.Empty;
        }

        // すべてのノードから対象id宛の接続を削除
        // Remove all incoming edges pointing to the target id
        private void RemoveIncomingConnections(int targetNodeId)
        {
            for (var i = 0; i < _connectNodes.Count; i++)
            {
                _connectNodes[i].RemoveAll(x => x.targetId == targetNodeId);
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
        private void AssignConnectionDestination(int nodeId, ConnectionDestination destination)
        {
            if (!IsWithinCurrentRange(nodeId))
            {
                EnsureNodeSlot(nodeId);
            }

            var current = _connectionDestinations[nodeId];
            if (!current.IsDefault)
            {
                _connectionDestinationToNodeId.Remove(current);
            }

            _connectionDestinations[nodeId] = destination;

            if (!destination.IsDefault)
            {
                _connectionDestinationToNodeId[destination] = nodeId;
            }
        }

        #endregion
    }
}
