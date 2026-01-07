using System;
using System.Collections.Generic;
using Client.Game.InGame.Entity.Factory;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Entity.Interface;
using MessagePack;
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
        ///     エンティティの最終更新時間をチェックし、一定時間経過していたら削除する
        /// </summary>
        private void Update()
        {
            // 1秒以上経過していたら削除
            var removeEntities = new List<long>();
            foreach (var entity in _entities)
                if ((DateTime.Now - entity.Value.lastUpdate).TotalSeconds > 1)
                    removeEntities.Add(entity.Key);

            foreach (var removeEntity in removeEntities)
            {
                // 削除対象エンティティが列車の場合は登録も解除する
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
        ///     エンティティの生成・更新を行う
        /// </summary>
        public void OnEntitiesUpdate(List<EntityResponse> entities)
        {
            foreach (var entity in entities)
            {
                // 既存エンティティの更新
                if (_entities.ContainsKey(entity.InstanceId))
                {
                    var cachedEntity = _entities[entity.InstanceId].objectEntity;
                    if (cachedEntity is not TrainCarEntityObject)
                    {
                        cachedEntity.SetPositionWithLerp(entity.Position);
                    }
                    // 列車はローカル側の姿勢更新処理に任せる
                    _entities[entity.InstanceId] = (DateTime.Now, cachedEntity);
                    continue;
                }
                
                // 新規エンティティの生成
                _entityObjectFactory.CreateEntity(transform, entity).ContinueWith(entityObject =>
                {
                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));

                    // 列車エンティティの場合は辞書へ登録する
                    if (entityObject is TrainCarEntityObject trainCarEntity)
                    {
                        _trainCars[trainCarEntity.TrainCarId] = trainCarEntity;
                    }
                    return entityObject;
                });
            }
        }

        // 現在存在する列車エンティティをまとめて取得する
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