using System;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Network.API;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     RailGraph差分の初期適用から再同期までを担うキャッシュ反映サービス
    ///     Service that applies the initial RailGraph snapshot and future resync payloads
    /// </summary>
    public sealed class RailGraphSnapshotApplier : IInitializable
    {
        private readonly RailGraphClientCache _cache;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;
        private readonly ClientStationReferenceRegistry _stationReferenceRegistry;
        private readonly TrainUnitTickState _tickState;

        public RailGraphSnapshotApplier(
            RailGraphClientCache cache,
            InitialHandshakeResponse initialHandshakeResponse,
            ClientStationReferenceRegistry stationReferenceRegistry,
            TrainUnitTickState tickState)
        {
            _cache = cache;
            _initialHandshakeResponse = initialHandshakeResponse;
            _stationReferenceRegistry = stationReferenceRegistry;
            _tickState = tickState;
        }

        public void Initialize()
        {
            // 初回ハンドシェイクのスナップショットを即座に適用
            // Apply the handshake snapshot immediately after construction
            ApplySnapshot(_initialHandshakeResponse?.RailGraphSnapshot);
        }

        public void ApplySnapshot(RailGraphSnapshotMessagePack snapshot)
        {
            if (snapshot != null &&
                TrainTickUnifiedIdUtility.CreateTickUnifiedId(snapshot.GraphTick, snapshot.GraphTickSequenceId) < _tickState.GetAppliedTickUnifiedId())
            {
                // 遅延rail snapshotが既に適用済み範囲より古い場合は破棄する。
                // Ignore delayed rail snapshots older than the applied sequence baseline.
                Debug.LogWarning(
                    "[RailGraphSnapshotApplier] Ignored stale rail snapshot. " +
                    $"graphTick={snapshot.GraphTick}, graphTickSequenceId={snapshot.GraphTickSequenceId}, " +
                    $"appliedTickUnifiedId={_tickState.GetAppliedTickUnifiedId()}");
                return;
            }

            // スナップショットが空のときは何もしない
            // Skip when snapshot payload is missing or empty
            if (snapshot?.Nodes == null || snapshot.Nodes.Count == 0)
            {
                return;
            }

            // ノードと辺の最大IDを算出し、配列サイズを先に確定
            // Determine the max node id before allocating buffers
            var maxNodeId = ResolveMaxNodeId(snapshot);
            if (maxNodeId < 0)
            {
                return;
            }

            // 事前確保したコンテナにノード情報を流し込み
            // Populate prepared containers with node information
            var size = maxNodeId + 1;
            // まとめてキャッシュへ反映
            // Commit prepared data to cache
            _cache.ApplySnapshot(snapshot, size);
            // 駅参照をキャッシュへ反映する
            // Apply station references to cache.
            _stationReferenceRegistry.ApplyStationReferences();

            #region 
            int ResolveMaxNodeId(RailGraphSnapshotMessagePack targetSnapshot)
            {
                // ノードから最大IDを探索
                // Look at nodes and edges to find the max node id
                var max = -1;
                foreach (var node in targetSnapshot.Nodes)
                {
                    if (node == null)
                        continue;
                    max = Math.Max(max, node.NodeId);
                }
                return max;
            }
            #endregion
        }
    }
}
