using System;
using System.Collections.Generic;
using Client.Game.InGame.Entity.Factory;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Entity
{
    public class EntityObjectDatastore : MonoBehaviour
    {
        private EntityObjectFactory _entityObjectFactory;
        private readonly Dictionary<long, (DateTime lastUpdate, IEntityObject objectEntity)> _entities = new();
        private readonly Dictionary<Guid, TrainCarEntityObject> _trainCars = new();
        
        private void Awake()
        {
            _entityObjectFactory = new EntityObjectFactory();
        }
        
        /// <summary>
        ///     繧ｨ繝ｳ繝・ぅ繝・ぅ譛邨よ峩譁ｰ譎る俣繧偵メ繧ｧ繝・け縺励∽ｸ螳壽凾髢鍋ｵ碁℃縺励※縺・◆繧牙炎髯､縺吶ｋ
        /// </summary>
        private void Update()
        {
            //1遘剃ｻ･荳顔ｵ碁℃縺励※縺・◆繧牙炎髯､
            var removeEntities = new List<long>();
            foreach (var entity in _entities)
                if ((DateTime.Now - entity.Value.lastUpdate).TotalSeconds > 1)
                    removeEntities.Add(entity.Key);
            foreach (var removeEntity in removeEntities)
            {
                // 列車エンティティ登録も同時に解除する
                // Remove train entity registration alongside deletion
                var removeTarget = _entities[removeEntity].objectEntity;
                if (removeTarget is TrainCarEntityObject trainCarEntity)
                {
                    _trainCars.Remove(trainCarEntity.TrainCarId);
                }
                removeTarget.Destroy();
                _entities.Remove(removeEntity);
            }
        }
        
        /// <summary>
        ///     繧ｨ繝ｳ繝・ぅ繝・ぅ縺ｮ逕滓・縲∵峩譁ｰ繧定｡後≧
        /// </summary>
        public void OnEntitiesUpdate(List<EntityResponse> entities)
        {
            foreach (var entity in entities)
            {
                // 譌｢蟄倥お繝ｳ繝・ぅ繝・ぅ縺ｮ譖ｴ譁ｰ
                // Update existing entity
                if (_entities.ContainsKey(entity.InstanceId))
                {
                    var cachedEntity = _entities[entity.InstanceId].objectEntity;
                    if (cachedEntity is not TrainCarEntityObject)
                    {
                        cachedEntity.SetPositionWithLerp(entity.Position);
                    }
                    // 列車はローカル姿勢更新に委譲する
                    // Leave trains to the local pose updater
                    _entities[entity.InstanceId] = (DateTime.Now, cachedEntity);
                    
                    continue;
                }
                
                
                // 譁ｰ隕上お繝ｳ繝・ぅ繝・ぅ縺ｮ逕滓・
                // Create new entity
                _entityObjectFactory.CreateEntity(transform, entity).ContinueWith(entityObject =>
                {
                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
                    // 列車エンティティを索引に登録する
                    // Register train entities into the lookup
                    if (entityObject is TrainCarEntityObject trainCarEntity)
                    {
                        _trainCars[trainCarEntity.TrainCarId] = trainCarEntity;
                    }
                    
                    return entityObject;
                });
            }
        }

        // 蛻苓ｻ翫お繝ｳ繝・ぅ繝・ぅをまとめて取得する
        // Collect the current train entity objects
        public void CopyTrainCarEntitiesTo(List<TrainCarEntityObject> buffer)
        {
            buffer.Clear();
            foreach (var trainCar in _trainCars.Values)
            {
                buffer.Add(trainCar);
            }
        }
    }
}
