using System.Collections.Generic;
using Client.Game.InGame.Train.Network;
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
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;

        public TrainUnitSnapshotApplier(
            TrainUnitClientCache cache,
            TrainUnitTickState tickState,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            InitialHandshakeResponse initialHandshakeResponse,
            TrainCarObjectDatastore trainCarDatastore)
        {
            _cache = cache;
            _tickState = tickState;
            _futureMessageBuffer = futureMessageBuffer;
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

            // スナップショットをモデルに変換して列車IDを収集する
            // Convert snapshots into models and collect train car ids
            var snapshotPacks = response.Snapshots;
            var bundles = new List<TrainUnitSnapshotBundle>(snapshotPacks?.Count ?? 0);
            var activeTrainCarInstanceIds = new HashSet<TrainCarInstanceId>();
            if (snapshotPacks != null)
            {
                for (var i = 0; i < snapshotPacks.Count; i++)
                {
                    var pack = snapshotPacks[i];
                    if (pack == null) continue;

                    var bundle = pack.ToModel();
                    bundles.Add(bundle);
                    CollectTrainCarInstanceIds(bundle, activeTrainCarInstanceIds);

                    // 車両オブジェクトを生成する
                    // Create train car objects
                    _trainCarDatastore.OnTrainObjectUpdate(bundle.Simulation.Cars);
                }
            }

            // キャッシュ更新後に不要な列車エンティティを除去する
            // Remove stale train entities after cache update
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

            _tickState.SetSnapshotBaseline(response.ServerTick, response.TickSequenceId);
            _futureMessageBuffer.DiscardUpToTickUnifiedId(snapshotTickUnifiedId);
            _futureMessageBuffer.RecordSnapshotAppliedTick(response.ServerTick);
            _trainCarDatastore.RemoveTrainEntitiesNotInSnapshot(activeTrainCarInstanceIds);

            #region Internal

            void CollectTrainCarInstanceIds(TrainUnitSnapshotBundle bundle, ISet<TrainCarInstanceId> target)
            {
                // 車両スナップショットからIDを集計する
                // Collect train car ids from the snapshot
                var cars = bundle.Simulation.Cars;
                if (cars == null) return;
                for (var i = 0; i < cars.Count; i++) target.Add(cars[i].TrainCarInstanceId);
            }

            #endregion
        }
    }
}
