using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameState.Implementation
{
    public class EntityRegistryImpl : IEntityRegistry
    {
        private readonly Dictionary<long, ClientEntityImpl> _entities = new();

        public EntityRegistryImpl()
        {
        }

        public IReadOnlyList<IClientEntity> GetEntities()
        {
            return _entities.Values.ToList();
        }

        public IClientEntity GetEntity(long instanceId)
        {
            return _entities.TryGetValue(instanceId, out var entity) ? entity : null;
        }

        public void UpdateEntity(long instanceId, string entityType, Vector3 position, string state)
        {
            if (!_entities.TryGetValue(instanceId, out var entity))
            {
                entity = new ClientEntityImpl(instanceId, entityType, position, state);
                _entities[instanceId] = entity;
            }
            else
            {
                entity.Update(position, state);
            }
        }

        public void RemoveEntity(long instanceId)
        {
            _entities.Remove(instanceId);
        }

        private class ClientEntityImpl : IClientEntity
        {
            private Vector3 _position;
            private string _state;

            public long InstanceId { get; }
            public string EntityType { get; }
            public Vector3 Position => _position;
            public string State => _state;

            public ClientEntityImpl(long instanceId, string entityType, Vector3 position, string state)
            {
                InstanceId = instanceId;
                EntityType = entityType;
                _position = position;
                _state = state;
            }

            public void Update(Vector3 position, string state)
            {
                _position = position;
                _state = state;
            }
        }
    }
}