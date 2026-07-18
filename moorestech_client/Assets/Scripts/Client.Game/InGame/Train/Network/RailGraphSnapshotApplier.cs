using System;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     RailGraph差分の初期適用から再同期までを担うキャッシュ反映サービス
    ///     Service that applies the initial RailGraph snapshot and future resync payloads
    /// </summary>
    public sealed class RailGraphSnapshotApplier
    {
        private readonly RailGraphClientCache _cache;
        private readonly ClientStationReferenceRegistry _stationReferenceRegistry;
        private readonly TrainUnitTickState _tickState;

        public RailGraphSnapshotApplier(
            RailGraphClientCache cache,
            ClientStationReferenceRegistry stationReferenceRegistry,
            TrainUnitTickState tickState)
        {
            _cache = cache;
            _stationReferenceRegistry = stationReferenceRegistry;
            _tickState = tickState;
        }

        public void ApplySnapshot(RailGraphSnapshotMessagePack snapshot)
        {
            // ペイロード欠損のみ無視。空Nodesは「レール全消滅」の正当なfull snapshotとして適用する
            // Skip only a missing payload; empty Nodes is a valid "all rails removed" full snapshot
            if (snapshot?.Nodes == null)
            {
                return;
            }

            var unifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(snapshot.GraphTick, snapshot.GraphTickSequenceId);
            if (unifiedId < _tickState.GetAppliedTickUnifiedId())
            {
                // 遅延rail snapshotが既に適用済み範囲より古い場合は破棄する。
                // Ignore delayed rail snapshots older than the applied sequence baseline.
                Debug.LogWarning(
                    "[RailGraphSnapshotApplier] Ignored stale rail snapshot. " +
                    $"graphTick={snapshot.GraphTick}, graphTickSequenceId={snapshot.GraphTickSequenceId}, " +
                    $"appliedTickUnifiedId={_tickState.GetAppliedTickUnifiedId()}");
                return;
            }

            // ノードの最大IDから配列サイズを確定（空snapshotはsize 0でキャッシュ全消去になる）
            // Size buffers from the max node id; an empty snapshot yields size 0 and clears the cache
            var maxNodeId = ResolveMaxNodeId(snapshot);
            var size = maxNodeId + 1;
            // まとめてキャッシュへ反映
            // Commit prepared data to cache
            _cache.ApplySnapshot(snapshot, size);
            // 駅参照をキャッシュへ反映する
            // Apply station references to cache.
            _stationReferenceRegistry.ApplyStationReferences();
            _tickState.RecordAppliedTickUnifiedId(unifiedId);

            #region Internal
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
