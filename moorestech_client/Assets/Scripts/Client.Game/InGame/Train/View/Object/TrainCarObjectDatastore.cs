using System.Collections.Generic;
using System.Linq;
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
        private readonly Dictionary<TrainCarInstanceId, TrainCarEntityObject> _entities = new();

        [Inject]
        public void Construct(TrainUnitClientCache trainUnitClientCache)
        {
            _carObjectFactory = new TrainCarObjectFactory(trainUnitClientCache);
        }

        public void OnTrainObjectUpdate(IReadOnlyList<TrainCarSnapshot> carSnapshots)
        {
            // 列車エンティティの生成・更新を反映する
            // Apply create/update for train entities
            for (var i = 0; i < carSnapshots.Count; i++)
            {
                var car = carSnapshots[i];
                if (_entities.ContainsKey(car.TrainCarInstanceId)) continue;

                // 新規車両のオブジェクトを生成する
                // Create object for new train car
                _carObjectFactory.CreateTrainCarObject(transform, car).ContinueWith(entityObject =>
                {
                    entityObject.Initialize();
                    _entities.Add(car.TrainCarInstanceId, entityObject);
                    return entityObject;
                });
            }
        }

        public void RemoveTrainEntitiesNotInSnapshot(IReadOnlyCollection<TrainCarInstanceId> activeTrainCarInstanceIds)
        {
            // スナップショットに存在しない列車エンティティを削除する
            // Remove train entities that are missing from the snapshot
            var removeIds = new List<TrainCarInstanceId>();
            foreach (var entry in _entities)
            {
                if (entry.Value == null) continue;
                if (!activeTrainCarInstanceIds.Contains(entry.Key)) removeIds.Add(entry.Key);
            }

            for (var i = 0; i < removeIds.Count; i++)
            {
                var trainCarInstanceId = removeIds[i];
                _entities[trainCarInstanceId].Destroy();
                _entities.Remove(trainCarInstanceId);
            }
        }

        // 指定TrainCarエンティティを削除する
        // Remove a single train car entity by id.
        public bool RemoveTrainEntity(TrainCarInstanceId trainCarInstanceId)
        {
            if (!_entities.TryGetValue(trainCarInstanceId, out var entity))
            {
                return false;
            }

            entity.Destroy();
            _entities.Remove(trainCarInstanceId);
            return true;
        }
    }
}
