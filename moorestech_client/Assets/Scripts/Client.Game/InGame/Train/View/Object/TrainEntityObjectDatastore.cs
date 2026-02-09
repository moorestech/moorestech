using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Entity.Factory;
using Client.Game.InGame.Train.Unit;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Train.View.Object
{
    public class TrainEntityObjectDatastore : MonoBehaviour
    {
        private TrainCarObjectFactory carObjectFactory;
        private readonly Dictionary<long, TrainCarEntityObject> _entities = new();

        [Inject]
        public void Construct(TrainUnitClientCache trainUnitClientCache)
        {
            carObjectFactory = new TrainCarObjectFactory(trainUnitClientCache);
        }

        public void OnTrainObjectUpdate(List<EntityResponse> entities)
        {
            // 列車エンティティの生成・更新を反映する。列車以外のチェックは行わない
            // Apply create/update for train entities
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                // EntityObjectDatastoreにならう
                // Following EntityObjectDatastore
                carObjectFactory.CreateTrainCarObject(transform, entity).ContinueWith(entityObject =>
                {
                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, entityObject);
                    return entityObject;
                });
            }
        }

        public void RemoveTrainEntitiesNotInSnapshot(IReadOnlyCollection<Guid> activeTrainCarIds)
        {
            // スナップショットに存在しない列車エンティティを削除する
            // Remove train entities that are missing from the snapshot
            var removeIds = new List<long>();
            foreach (var entry in _entities)
            {
                var trainEntity = entry.Value as TrainCarEntityObject;
                if (trainEntity == null) continue;
                if (!activeTrainCarIds.Contains(trainEntity.TrainCarId)) removeIds.Add(entry.Key);
            }

            for (var i = 0; i < removeIds.Count; i++)
            {
                var entityId = removeIds[i];
                _entities[entityId].Destroy();
                _entities.Remove(entityId);
            }
        }
    }
}
