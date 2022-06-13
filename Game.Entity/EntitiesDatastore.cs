using System;
using System.Collections.Generic;
using Game.Entity.Interface;

namespace Game.Entity
{
    public class EntitiesDatastore : IEntitiesDatastore
    {
        //todo セーブとロードを実装する
        private readonly Dictionary<long,IEntity> _entities = new();
        public void Add(IEntity entity)
        {
            _entities.Add(entity.InstanceId, entity);
        }

        public bool Exists(long instanceId)
        {
            return _entities.ContainsKey(instanceId);
        }

        public void SetPosition(long instanceId, ServerVector3 position)
        {
            if (_entities.TryGetValue(instanceId, out var entity))
            {
                entity.SetPosition(position);
                return;
            }
            throw new Exception("Entity not found " + instanceId);
        }

        public ServerVector3 GetPosition(long instanceId)
        {
            if (_entities.TryGetValue(instanceId, out var entity))
            {
                return entity.Position;
            }
            throw new Exception("Entity not found " + instanceId);
        }
    }
}