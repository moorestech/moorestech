using System.Collections.Generic;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Client.Network.API;
using Game.Train.Unit;
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
            _tickState.SetSnapshotBaselineTick(response.ServerTick);
            _futureMessageBuffer.DiscardUpToTick(response.ServerTick);
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
