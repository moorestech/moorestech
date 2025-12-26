using System;
using System.Collections.Generic;
using Client.Game.InGame.Entity.Factory;
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
        ///     エンティティ最終更新時間をチェックし、一定時間経過していたら削除する
        /// </summary>
        private void Update()
        {
            //1秒以上経過していたら削除
            var removeEntities = new List<long>();
            foreach (var entity in _entities)
                if ((DateTime.Now - entity.Value.lastUpdate).TotalSeconds > 1)
                    removeEntities.Add(entity.Key);
            foreach (var removeEntity in removeEntities)
            {
                _entities[removeEntity].objectEntity.Destroy();
                _entities.Remove(removeEntity);
            }
        }
        
        /// <summary>
        ///     エンティティの生成、更新を行う
        /// </summary>
        public void OnEntitiesUpdate(List<EntityResponse> entities)
        {
            foreach (var entity in entities)
            {
                // 既存エンティティの更新
                // Update existing entity
                if (_entities.ContainsKey(entity.InstanceId))
                {
                    var objectEntity = _entities[entity.InstanceId].objectEntity;
                    if (entity.Type == VanillaEntityType.VanillaItem)
                    {
                        UpdateBeltConveyorItemEntity(entity, objectEntity, true);
                        _entities[entity.InstanceId] = (DateTime.Now, objectEntity);
                        continue;
                    }
                    
                    objectEntity.SetPositionWithLerp(entity.Position);
                    _entities[entity.InstanceId] = (DateTime.Now, objectEntity);
                    
                    continue;
                }
                
                
                // 新規エンティティの生成
                // Create new entity
                _entityObjectFactory.CreateEntity(transform, entity).ContinueWith(entityObject =>
                {
                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
                    
                    return entityObject;
                });
            }
        }
        
        private static void UpdateBeltConveyorItemEntity(EntityResponse entity, IEntityObject entityObject, bool useLerp)
        {
            // EntityDataからベルトアイテムの位置情報を復元する
            // Restore belt item position from EntityData
            if (entityObject is not IBeltConveyorItemEntityObject beltEntity) return;
            if (entity.EntityData == null || entity.EntityData.Length == 0) return;
            
            var state = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(entity.EntityData);
            beltEntity.SetBeltConveyorItemPosition(state, useLerp);
        }
    }
}
