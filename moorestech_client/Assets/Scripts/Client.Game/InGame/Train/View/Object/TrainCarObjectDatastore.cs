using System.Collections.Generic;
using Client.Game.InGame.Entity.Factory;
using Client.Game.InGame.Train.Unit;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Train.View.Object
{
    public class TrainCarObjectDatastore : MonoBehaviour
    {
        private TrainCarObjectFactory _carObjectFactory;
        private TrainUnitClientCache _trainUnitClientCache;
        private readonly Dictionary<TrainCarInstanceId, TrainCarEntityObject> _entities = new();
        private readonly HashSet<TrainCarInstanceId> _highlightedTrainCars = new();
        private readonly HashSet<TrainCarInstanceId> _activeTrainCars = new();
        private readonly HashSet<TrainCarInstanceId> _pendingCreation = new();

        [Inject]
        public void Construct(TrainUnitClientCache trainUnitClientCache)
        {
            _trainUnitClientCache = trainUnitClientCache;
            _carObjectFactory = new TrainCarObjectFactory(trainUnitClientCache);
        }

        public void OnTrainObjectUpdate(IReadOnlyList<TrainCarSnapshot> carSnapshots)
        {
            // 列車エンティティの生成・更新を反映する
            // Apply create/update for train entities
            for (var i = 0; i < carSnapshots.Count; i++)
            {
                var carSnapshot = carSnapshots[i];
                _activeTrainCars.Add(carSnapshot.TrainCarInstanceId);
                CreateTrainEntityIfMissing(carSnapshot);
            }
        }

        public void RemoveTrainEntitiesNotInSnapshot(IReadOnlyCollection<TrainCarInstanceId> activeTrainCarInstanceIds)
        {
            // スナップショットに存在しない列車エンティティを削除する
            // Remove train entities that are missing from the snapshot
            var activeIdSet = new HashSet<TrainCarInstanceId>(activeTrainCarInstanceIds);
            _activeTrainCars.Clear();
            foreach (var activeId in activeIdSet)
            {
                _activeTrainCars.Add(activeId);
            }
            var removeIds = CollectMissingEntityIds(activeIdSet);
            RemoveEntities(removeIds);
        }

        // 指定TrainCarエンティティを削除する
        // Remove a single train car entity by id.
        public bool RemoveTrainEntity(TrainCarInstanceId trainCarInstanceId)
        {
            if (!_entities.TryGetValue(trainCarInstanceId, out var entity))
            {
                _activeTrainCars.Remove(trainCarInstanceId);
                return false;
            }

            RemoveEntity(trainCarInstanceId, entity);
            return true;
        }

        // 設置候補と重なったTrainUnitを可視化する
        // Highlight train units that overlap with placement candidates
        public void SetPlacementOverlapHighlight(IReadOnlyCollection<TrainInstanceId> overlapTrainIds)
        {
            // 重複対象がなければ終了する
            // Exit early when no overlap targets are provided
            if (overlapTrainIds == null || overlapTrainIds.Count == 0)
            {
                ClearAllHighlight();
                return;
            }
            var overlapTrainIdSet = new HashSet<TrainInstanceId>(overlapTrainIds);

            // 可読性優先で毎フレーム再適用する
            // Re-apply highlights every frame for readability
            var overlapCarIds = CollectOverlapCarIds(overlapTrainIdSet);
            ClearAllHighlight();
            ApplyHighlight(overlapCarIds);
        }

        public void ClearPlacementOverlapHighlight()
        {
            ClearAllHighlight();
        }

        private void ClearAllHighlight()
        {
            if (_highlightedTrainCars.Count <= 0)
            {
                return;
            }
            var removeIds = new List<TrainCarInstanceId>(_highlightedTrainCars);
            for (var i = 0; i < removeIds.Count; i++)
            {
                var removeId = removeIds[i];
                if (_entities.TryGetValue(removeId, out var entity) && entity != null)
                {
                    entity.ResetMaterial();
                }
            }
            _highlightedTrainCars.Clear();
        }

        private void CreateTrainEntityIfMissing(TrainCarSnapshot carSnapshot)
        {
            var trainCarInstanceId = carSnapshot.TrainCarInstanceId;
            if (_entities.ContainsKey(trainCarInstanceId) || _pendingCreation.Contains(trainCarInstanceId))
            {
                return;
            }

            // 新規車両の生成処理を予約する
            // Reserve async creation for a new train car
            _pendingCreation.Add(trainCarInstanceId);
            CreateAndRegisterAsync(trainCarInstanceId, carSnapshot).Forget();

            #region Internal

            // 生成完了後にエンティティを登録する
            // Register entity after async creation completes
            async UniTaskVoid CreateAndRegisterAsync(TrainCarInstanceId targetId, TrainCarSnapshot snapshot)
            {
                try
                {
                    var entityObject = await _carObjectFactory.CreateTrainCarObject(transform, snapshot);

                    // 生成対象が最新スナップショットで無効なら登録しない
                    // Skip registration when the target car is no longer active
                    if (!_activeTrainCars.Contains(targetId))
                    {
                        entityObject.Destroy();
                        return;
                    }

                    // 先に登録済みなら重複オブジェクトを破棄する
                    // Destroy duplicate object if another registration already exists
                    if (_entities.ContainsKey(targetId))
                    {
                        entityObject.Destroy();
                        return;
                    }

                    entityObject.Initialize();
                    _entities[targetId] = entityObject;
                }
                finally
                {
                    _pendingCreation.Remove(targetId);
                }
            }

            #endregion
        }

        private List<TrainCarInstanceId> CollectMissingEntityIds(ISet<TrainCarInstanceId> activeIdSet)
        {
            var removeIds = new List<TrainCarInstanceId>();
            foreach (var entry in _entities)
            {
                if (entry.Value == null)
                {
                    continue;
                }
                if (!activeIdSet.Contains(entry.Key))
                {
                    removeIds.Add(entry.Key);
                }
            }
            return removeIds;
        }

        private void RemoveEntities(IReadOnlyList<TrainCarInstanceId> removeIds)
        {
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
            if (entity != null)
            {
                entity.Destroy();
            }
            _entities.Remove(trainCarInstanceId);
            _highlightedTrainCars.Remove(trainCarInstanceId);
            _activeTrainCars.Remove(trainCarInstanceId);
        }

        private HashSet<TrainCarInstanceId> CollectOverlapCarIds(ISet<TrainInstanceId> overlapTrainIds)
        {
            var overlapCarIds = new HashSet<TrainCarInstanceId>();
            foreach (var pair in _entities)
            {
                var trainCarInstanceId = pair.Key;
                if (!_trainUnitClientCache.TryGetCarSnapshot(trainCarInstanceId, out var unit, out _, out _, out _))
                {
                    continue;
                }
                if (!overlapTrainIds.Contains(unit.TrainInstanceId))
                {
                    continue;
                }
                overlapCarIds.Add(trainCarInstanceId);
            }
            return overlapCarIds;
        }

        private void ApplyHighlight(ISet<TrainCarInstanceId> overlapCarIds)
        {
            foreach (var carId in overlapCarIds)
            {
                if (!_entities.TryGetValue(carId, out var entity) || entity == null)
                {
                    continue;
                }
                entity.SetPlacementOverlapPreviewing();
                _highlightedTrainCars.Add(carId);
            }
        }
    }
}
