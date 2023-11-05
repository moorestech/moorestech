using System;
using System.Collections.Generic;
using Game.Entity.Interface;
using UnityEngine;

namespace Game.Entity
{
    public class EntitiesDatastore : IEntitiesDatastore
    {
        private readonly Dictionary<long, IEntity> _entities = new();

        private readonly IEntityFactory _entityFactory;

        public EntitiesDatastore(IEntityFactory entityFactory)
        {
            _entityFactory = entityFactory;
        }

        public void Add(IEntity entity)
        {
            _entities.Add(entity.InstanceId, entity);
        }

        public bool Exists(long instanceId)
        {
            return _entities.ContainsKey(instanceId);
        }

        public IEntity Get(long instanceId)
        {
            return _entities[instanceId];
        }

        public List<SaveEntityData> GetSaveBlockDataList()
        {
            var saveData = new List<SaveEntityData>();
            foreach (var entity in _entities)
            {
                var e = entity.Value;
                saveData.Add(new SaveEntityData(e.EntityType, e.InstanceId, e.Position));
            }

            return saveData;
        }

        public void LoadBlockDataList(List<SaveEntityData> saveBlockDataList)
        {
            foreach (var save in saveBlockDataList)
            {
                var entity = _entityFactory.CreateEntity(save.Type, save.InstanceId);
                _entities.Add(entity.InstanceId, entity);

                var pos = new Vector3(save.X, save.Y, save.Z);
                SetPosition(save.InstanceId, pos);
            }
        }

        public void SetPosition(long instanceId, Vector3 position)
        {
            if (_entities.TryGetValue(instanceId, out var entity))
            {
                entity.SetPosition(position);
                return;
            }

            throw new Exception("Entity not found " + instanceId);
        }

        public Vector3 GetPosition(long instanceId)
        {
            if (_entities.TryGetValue(instanceId, out var entity)) return entity.Position;
            throw new Exception("Entity not found " + instanceId);
        }
    }
}