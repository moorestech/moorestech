using System;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Network.API;
using UnityEngine;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Core.Update.TickSynchronization;

namespace Client.Game.InGame.Train.View
{
    /// <summary>
    ///     列車スナップショットを初期化時にキャッシュへ流し込むアプライヤー
    ///     Applies and verifies initial train snapshots in the local cache
    /// </summary>
    public sealed class TrainUnitSnapshotApplier
    {
        private readonly TrainUnitClientCache _cache;
        private readonly TrainTickContext _context;
        private readonly TrainCarObjectDatastore _trainCarDatastore;

        public TrainUnitSnapshotApplier(
            TrainUnitClientCache cache,
            TrainTickContext context,
            TrainCarObjectDatastore trainCarDatastore)
        {
            _cache = cache;
            _context = context;
            _trainCarDatastore = trainCarDatastore;
        }

        // レスポンスに含まれる列車データをキャッシュへ適用
        // Apply the received snapshot response to the cache
        public void ApplySnapshot(TrainUnitSnapshotResponse response)
        {
            if (response?.Snapshots == null) throw new InvalidOperationException("Initial train snapshot list is missing.");
            var snapshotTickUnifiedId = TickUnifiedIdUtility.CreateTickUnifiedId(response.ServerTick, response.TickSequenceId);
            Debug.Log("ApplySnapshotTrainUnit: " + response.ServerTick + "_" + response.TickSequenceId);
            
            if (snapshotTickUnifiedId < _context.State.GetAppliedTickUnifiedId())
            {
                throw new InvalidOperationException($"Initial train snapshot watermark is stale: received={snapshotTickUnifiedId}, applied={_context.State.GetAppliedTickUnifiedId()}");
            }

            var bundles = response.Snapshots;

            // full snapshotはcacheとviewを同じ単位で全差し替えする
            // Replace both cache and views as one full-snapshot boundary
            _cache.OverrideAll(bundles);
            var localHashAfterApply = _cache.ComputeCurrentHash();
            if (localHashAfterApply != response.UnitsHash)
            {
                // 初期適用直後のhash差分を検知して原因切り分けに使う
                // Detect hash differences right after snapshot apply for root-cause isolation.
                throw new InvalidOperationException(
                    "[TrainUnitSnapshotApplier] Snapshot hash mismatch right after apply. " +
                    $"serverTick={response.ServerTick}, snapshotCount={bundles.Count}, " +
                    $"serverHash={response.UnitsHash}, clientHash={localHashAfterApply}, cacheTrainCount={_cache.Units.Count}");
            }

            // cache更新後に列車表示オブジェクトを全再生成する
            // Recreate all train view objects after cache replacement
            _trainCarDatastore.RecreateAllTrainEntities(bundles);
            _context.State.RecordAppliedTickUnifiedId(snapshotTickUnifiedId);
        }
    }
}
