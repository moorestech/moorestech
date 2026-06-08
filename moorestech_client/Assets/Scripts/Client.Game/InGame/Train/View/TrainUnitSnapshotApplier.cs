using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Client.Network.API;
using Game.Train.Unit;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.View
{
    /// <summary>
    ///     列車スナップショットを初期化時にキャッシュへ流し込むアプライヤー
    ///     Applies the initial train snapshots to the local cache and can be reused for resync
    /// </summary>
    public sealed class TrainUnitSnapshotApplier : IInitializable
    {
        private readonly TrainUnitClientCache _cache;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;

        public TrainUnitSnapshotApplier(
            TrainUnitClientCache cache,
            TrainUnitTickState tickState,
            InitialHandshakeResponse initialHandshakeResponse,
            TrainCarObjectDatastore trainCarDatastore)
        {
            _cache = cache;
            _tickState = tickState;
            _initialHandshakeResponse = initialHandshakeResponse;
            _trainCarDatastore = trainCarDatastore;
        }

        public void Initialize()
        {
            ApplySnapshot(_initialHandshakeResponse?.TrainUnitSnapshots);
        }

        // レスポンスに含まれる列車データをキャッシュへ適用
        // Apply the received snapshot response to the cache
        public void ApplySnapshot(TrainUnitSnapshotResponse response)
        {
            if (response == null) return;
            var snapshotTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(response.ServerTick, response.TickSequenceId);
            Debug.Log("ApplySnapshotTrainUnit: " + response.ServerTick + "_" + response.TickSequenceId);
            
            if (snapshotTickUnifiedId < _tickState.GetAppliedTickUnifiedId())
            {
                // 遅延して届いた古いsnapshotは適用せず破棄する。
                // Ignore delayed snapshots that are older than the applied sequence baseline.
                Debug.LogWarning(
                    "[TrainUnitSnapshotApplier] Ignored stale snapshot response. " +
                    $"serverTick={response.ServerTick}, tickSequenceId={response.TickSequenceId}, " +
                    $"snapshotTickUnifiedId={snapshotTickUnifiedId}, appliedTickUnifiedId={_tickState.GetAppliedTickUnifiedId()}");
                return;
            }

            // スナップショットをモデルに変換する
            // Convert received snapshots into model bundles
            var snapshots = response.Snapshots;
            var bundles = new List<TrainUnitSnapshotBundle>(snapshots?.Count ?? 0);
            if (snapshots != null)
            {
                for (var i = 0; i < snapshots.Count; i++)
                {
                    var bundle = snapshots[i];
                    bundles.Add(bundle);
                }
            }

            // full snapshotはcacheとviewを同じ単位で全差し替えする
            // Replace both cache and views as one full-snapshot boundary
            _cache.OverrideAll(bundles);
            var localHashAfterApply = _cache.ComputeCurrentHash();
            if (localHashAfterApply != response.UnitsHash)
            {
                // 初期適用直後のhash差分を検知して原因切り分けに使う
                // Detect hash differences right after snapshot apply for root-cause isolation.
                Debug.LogWarning(
                    "[TrainUnitSnapshotApplier] Snapshot hash mismatch right after apply. " +
                    $"serverTick={response.ServerTick}, snapshotCount={bundles.Count}, " +
                    $"serverHash={response.UnitsHash}, clientHash={localHashAfterApply}, cacheTrainCount={_cache.Units.Count}");
            }

            // cache更新後に列車表示オブジェクトを全再生成する
            // Recreate all train view objects after cache replacement
            _trainCarDatastore.RecreateAllTrainEntities(bundles);
            _tickState.RecordAppliedTickUnifiedId(snapshotTickUnifiedId);
        }
    }
}
