using System;
using System.Collections.Generic;
using Game.Entity.Interface;
using UnityEngine;

namespace Game.Entity
{
    public class EntitiesDatastore : IEntitiesDatastore
    {
        private readonly Dictionary<EntityInstanceId, IEntity> _entities = new();
        
        private readonly IEntityFactory _entityFactory;
        
        public EntitiesDatastore(IEntityFactory entityFactory)
        {
            _entityFactory = entityFactory;
        }
        
        public void Add(IEntity entity)
        {
            _entities.Add(entity.InstanceId, entity);
        }
        
        public bool Exists(EntityInstanceId instanceId)
        {
            return _entities.ContainsKey(instanceId);
        }
        
        public IEntity Get(EntityInstanceId instanceId)
        {
            return _entities[instanceId];
        }
        
        public List<EntityJsonObject> GetSaveJsonObject()
        {
            var saveData = new List<EntityJsonObject>();
            foreach (KeyValuePair<EntityInstanceId, IEntity> entity in _entities)
            {
                var e = entity.Value;
                saveData.Add(new EntityJsonObject(e.EntityType, e.InstanceId.AsPrimitive(), e.Position));
            }
            
            return saveData;
        }
        
        public void LoadBlockDataList(List<EntityJsonObject> saveBlockDataList)
        {
            foreach (var save in saveBlockDataList)
            {
                var entity = _entityFactory.LoadEntity(save.Type, new EntityInstanceId(save.InstanceId));
                _entities.Add(entity.InstanceId, entity);
                
                var pos = new Vector3(save.X, save.Y, save.Z);
                SetPosition(new EntityInstanceId(save.InstanceId), pos);
            }
        }
        
        public void SetPosition(EntityInstanceId instanceId, Vector3 position)
        {
            if (_entities.TryGetValue(instanceId, out var entity))
            {
                entity.SetPosition(position);
                return;
            }
            
            throw new Exception("Entity not found " + instanceId);
        }
        
        public Vector3 GetPosition(EntityInstanceId instanceId)
        {
            if (_entities.TryGetValue(instanceId, out var entity)) return entity.Position;
            throw new Exception("Entity not found " + instanceId);
        }
    }
}