using System;
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
        private readonly Dictionary<Guid, TrainCarEntityObject> _entities = new();

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
                if (_entities.ContainsKey(car.TrainCarInstanceGuid)) continue;

                // 新規車両のオブジェクトを生成する
                // Create object for new train car
                _carObjectFactory.CreateTrainCarObject(transform, car).ContinueWith(entityObject =>
                {
                    entityObject.Initialize();
                    _entities.Add(car.TrainCarInstanceGuid, entityObject);
                    return entityObject;
                });
            }
        }

        public void RemoveTrainEntitiesNotInSnapshot(IReadOnlyCollection<Guid> activeTrainCarIds)
        {
            // スナップショットに存在しない列車エンティティを削除する
            // Remove train entities that are missing from the snapshot
            var removeIds = new List<Guid>();
            foreach (var entry in _entities)
            {
                if (entry.Value == null) continue;
                if (!activeTrainCarIds.Contains(entry.Key)) removeIds.Add(entry.Key);
            }

            for (var i = 0; i < removeIds.Count; i++)
            {
                var carId = removeIds[i];
                _entities[carId].Destroy();
                _entities.Remove(carId);
            }
        }
    }
}
