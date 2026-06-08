using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Game.Train.Unit;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Train.View.Object
{
    public class TrainCarObjectDatastore : MonoBehaviour
    {
        public IObservable<UniRx.Unit> OnInitializeComplete => _initializeCompleteSubject;

        private readonly UniRx.Subject<UniRx.Unit> _initializeCompleteSubject = new();
        private readonly Dictionary<TrainCarInstanceId, TrainCarEntityObject> _entities = new();

        private TrainCarObjectFactory _carObjectFactory;

        public event Action<TrainCarInstanceId> TrainCarEntityRemoving;

        [Inject]
        public void Construct(TrainUnitClientCache trainUnitClientCache, TrainUnitTickState tickState)
        {
            // datastoreはfactoryを保持し、表示状態そのものは管理しない
            // The datastore holds the factory and does not manage visual state
            _carObjectFactory = new TrainCarObjectFactory(trainUnitClientCache, tickState);
        }

        public void OnTrainObjectUpdate(IReadOnlyList<TrainCarSnapshot> carSnapshots)
        {
            // snapshotに存在するcar entityを即時生成する
            // Create train car entities in the snapshot immediately
            for (var i = 0; i < carSnapshots.Count; i++)
            {
                var carSnapshot = carSnapshots[i];
                CreateTrainEntityIfMissing(carSnapshot);
            }
            _initializeCompleteSubject.OnNext(UniRx.Unit.Default);
        }

        public void RecreateAllTrainEntities(IReadOnlyList<TrainUnitSnapshotBundle> snapshots)
        {
            // full snapshotではcache側のunit差し替えに合わせてviewも全再生成する
            // Recreate all views on full snapshots to match replaced cached train units
            var removeIds = CollectEntityIds();
            RemoveEntities(removeIds);

            // 最新snapshotに含まれるcarだけを同期生成する
            // Synchronously create only cars contained in the latest snapshot
            for (var i = 0; i < snapshots.Count; i++)
            {
                var cars = snapshots[i].Simulation.Cars;
                if (cars == null)
                {
                    continue;
                }
                for (var j = 0; j < cars.Count; j++)
                {
                    var carSnapshot = cars[j];
                    CreateTrainEntityIfMissing(carSnapshot);
                }
            }
            _initializeCompleteSubject.OnNext(UniRx.Unit.Default);
        }

        public bool RemoveTrainEntity(TrainCarInstanceId trainCarInstanceId)
        {
            // 指定car entityが存在しなければ何もしない
            // Do nothing when the target entity does not exist
            if (!_entities.TryGetValue(trainCarInstanceId, out var entity))
            {
                return false;
            }

            RemoveEntity(trainCarInstanceId, entity);
            return true;
        }

        public bool TryGetEntity(TrainCarInstanceId id, out TrainCarEntityObject entity)
        {
            if (!_entities.TryGetValue(id, out entity))
            {
                entity = null;
                return false;
            }

            return entity != null;
        }

        private void CreateTrainEntityIfMissing(TrainCarSnapshot carSnapshot)
        {
            var trainCarInstanceId = carSnapshot.TrainCarInstanceId;
            if (_entities.ContainsKey(trainCarInstanceId))
            {
                return;
            }

            // factoryはPrefab cacheから同期生成して、その場で登録を完了する
            // The factory synchronously creates from the Prefab cache and registers immediately
            var entityObject = _carObjectFactory.CreateTrainCarObject(transform, carSnapshot);
            _entities[trainCarInstanceId] = entityObject;
        }

        private List<TrainCarInstanceId> CollectEntityIds()
        {
            var removeIds = new List<TrainCarInstanceId>(_entities.Count);
            foreach (var entry in _entities)
            {
                removeIds.Add(entry.Key);
            }
            return removeIds;
        }

        private void RemoveEntities(IReadOnlyList<TrainCarInstanceId> removeIds)
        {
            // 収集済みIDだけを順に削除する
            // Remove only the ids collected beforehand
            for (var i = 0; i < removeIds.Count; i++)
            {
                var trainCarInstanceId = removeIds[i];
                if (!_entities.TryGetValue(trainCarInstanceId, out var entity))
                {
                    continue;
                }
                RemoveEntity(trainCarInstanceId, entity);
            }
        }

        private void RemoveEntity(TrainCarInstanceId trainCarInstanceId, TrainCarEntityObject entity)
        {
            // 削除通知後にview objectと管理情報を破棄する
            // Notify listeners before destroying the view object and bookkeeping
            TrainCarEntityRemoving?.Invoke(trainCarInstanceId);
            if (entity != null)
            {
                entity.Destroy();
            }
            _entities.Remove(trainCarInstanceId);
        }
    }
}
