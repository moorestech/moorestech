using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameState.Implementation
{
    public class EntityRegistry : IEntityRegistry, IVanillaApiConnectable, IVanillaApiPollable, IDisposable
    {
        private readonly Dictionary<long, ClientEntityImpl> _entities = new();

        public EntityRegistry()
        {
        }
        
        public void ConnectToVanillaApi(InitialHandshakeResponse initialHandshakeResponse)
        {
            
            // Initialize entities from handshake response
            var worldData = initialHandshakeResponse.WorldData;
            foreach (var entityResponse in worldData.Entities)
            {
                UpdateEntity(entityResponse.InstanceId, entityResponse.Type, entityResponse.Position, entityResponse.State);
            }
            
        }
        
        public async UniTask UpdateWithWorldData(WorldDataResponse worldData)
        {
            // Update entities
            var currentEntityIds = new HashSet<long>(_entities.Keys);
            var newEntityIds = new HashSet<long>();
            
            foreach (var entityResponse in worldData.Entities)
            {
                newEntityIds.Add(entityResponse.InstanceId);
                UpdateEntity(entityResponse.InstanceId, entityResponse.Type, entityResponse.Position, entityResponse.State);
            }
            
            // Remove entities that no longer exist
            currentEntityIds.ExceptWith(newEntityIds);
            foreach (var removedId in currentEntityIds)
            {
                RemoveEntity(removedId);
            }
        }
        
        public void Dispose()
        {
            // No resources to dispose anymore since polling moved to GameStateManager
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