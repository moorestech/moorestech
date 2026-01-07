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
                var removeTarget = _entities[removeEntity].objectEntity;
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
                    cachedEntity.SetPositionWithLerp(entity.Position);
                    continue;
                }
                
                // 新規エンティティの生成
                _entityObjectFactory.CreateEntity(transform, entity).ContinueWith(entityObject =>
                {
                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
                    return entityObject;
                });
            }
        }
    }
}