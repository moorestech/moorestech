using System;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Server.Util.MessagePack;
using UnityEngine;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Core.Update.TickSynchronization;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     初期RailGraph snapshotのキャッシュ反映サービス
    ///     Applies and verifies the initial RailGraph snapshot
    /// </summary>
    public sealed class RailGraphSnapshotApplier
    {
        private readonly RailGraphClientCache _cache;
        private readonly ClientStationReferenceRegistry _stationReferenceRegistry;
        private readonly TrainTickContext _context;

        public RailGraphSnapshotApplier(
            RailGraphClientCache cache,
            ClientStationReferenceRegistry stationReferenceRegistry,
            TrainTickContext context)
        {
            _cache = cache;
            _stationReferenceRegistry = stationReferenceRegistry;
            _context = context;
        }

        public void ApplySnapshot(RailGraphSnapshotMessagePack snapshot)
        {
            // 欠損payloadを拒否し、空Nodesは有効な初期状態として適用する
            // Reject missing payloads; empty Nodes is a valid initial state
            if (snapshot?.Nodes == null || snapshot.Connections == null)
            {
                throw new InvalidOperationException("Initial rail snapshot payload, nodes or connections are missing.");
            }

            var unifiedId = TickUnifiedIdUtility.CreateTickUnifiedId(snapshot.GraphTick, snapshot.GraphTickSequenceId);
            if (unifiedId < _context.State.GetAppliedTickUnifiedId())
            {
                throw new InvalidOperationException($"Initial rail snapshot watermark is stale: received={unifiedId}, applied={_context.State.GetAppliedTickUnifiedId()}");
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
            var actualHash = _cache.ComputeCurrentHash();
            if (actualHash != snapshot.GraphHash)
                throw new InvalidOperationException($"Initial rail hash mismatch: tick={snapshot.GraphTick}_{snapshot.GraphTickSequenceId}, expected={snapshot.GraphHash}, actual={actualHash}");
            _context.State.RecordAppliedTickUnifiedId(unifiedId);

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
