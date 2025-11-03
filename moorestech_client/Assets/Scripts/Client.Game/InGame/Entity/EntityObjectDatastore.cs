using System;
using System.Collections.Generic;
using Client.Network.API;
using UnityEngine;

namespace Client.Game.InGame.Entity
{
    public class EntityObjectDatastore : MonoBehaviour
    {
        [SerializeField] private EntityObjectFactory entityObjectFactory;
        
        private readonly Dictionary<long, (DateTime lastUpdate, IEntityObject objectEntity)> _entities = new();
        
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
                if (_entities.ContainsKey(entity.InstanceId))
                {
                    _entities[entity.InstanceId].objectEntity.SetInterpolationPosition(entity.Position);
                    _entities[entity.InstanceId] = (DateTime.Now, _entities[entity.InstanceId].objectEntity);
                }
                else
                {
                    var entityObject = entityObjectFactory.CreateEntity(entity);
                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
                }
        }
    }
}