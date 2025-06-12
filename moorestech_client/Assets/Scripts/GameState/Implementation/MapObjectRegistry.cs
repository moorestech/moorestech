using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace GameState.Implementation
{
    public class MapObjectRegistry : IMapObjectRegistry, IVanillaApiConnectable
    {
        private readonly Dictionary<int, ReadOnlyMapObjectImpl> _mapObjects = new();

        public MapObjectRegistry()
        {
        }
        
        public void ConnectToVanillaApi(InitialHandshakeResponse initialHandshakeResponse)
        {
            
            // Initialize map objects from handshake response
            var mapObjectInfos = initialHandshakeResponse.MapObjects;
            foreach (var mapObjectInfo in mapObjectInfos)
            {
                // TODO: MapObjectsInfoMessagePack currently only contains InstanceId and IsDestroyed
                // Need to get MapObjectId and Position from elsewhere or update the protocol
                // For now, we'll skip initialization and rely on events
                if (mapObjectInfo.IsDestroyed)
                {
                    // Create a placeholder destroyed map object
                    AddOrUpdateMapObject(
                        mapObjectInfo.InstanceId, 
                        0, // MapObjectId not available
                        Vector3.zero, // Position not available
                        true);
                }
            }
            
            // Subscribe to map object events
            SubscribeToMapObjectEvents();
        }
        
        private void SubscribeToMapObjectEvents()
        {
            // Map object update event (currently only handles destruction)
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MapObjectUpdateEventPacket.EventTag, payload =>
            {
                var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(payload);
                
                if (data.EventType == MapObjectUpdateEventMessagePack.DestroyEventType)
                {
                    // Mark the map object as mined/destroyed
                    if (_mapObjects.TryGetValue(data.InstanceId, out var mapObject))
                    {
                        mapObject.Update(mapObject.Position, true);
                    }
                }
            });
        }

        public IReadOnlyMapObject GetMapObject(int instanceId)
        {
            return _mapObjects.TryGetValue(instanceId, out var mapObject) ? mapObject : null;
        }

        public IReadOnlyDictionary<int, IReadOnlyMapObject> AllMapObjects
        {
            get
            {
                var result = new Dictionary<int, IReadOnlyMapObject>();
                foreach (var kvp in _mapObjects)
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }
        }

        public void AddOrUpdateMapObject(int instanceId, int mapObjectId, Vector3 position, bool isMined)
        {
            if (!_mapObjects.TryGetValue(instanceId, out var mapObject))
            {
                mapObject = new ReadOnlyMapObjectImpl(instanceId, mapObjectId, position, isMined);
                _mapObjects[instanceId] = mapObject;
            }
            else
            {
                mapObject.Update(position, isMined);
            }
        }

        public void RemoveMapObject(int instanceId)
        {
            _mapObjects.Remove(instanceId);
        }

        private class ReadOnlyMapObjectImpl : IReadOnlyMapObject
        {
            private Vector3 _position;
            private bool _isMined;

            public int InstanceId { get; }
            public int MapObjectId { get; }
            public Vector3 Position => _position;
            public bool IsMined => _isMined;

            public ReadOnlyMapObjectImpl(int instanceId, int mapObjectId, Vector3 position, bool isMined)
            {
                InstanceId = instanceId;
                MapObjectId = mapObjectId;
                _position = position;
                _isMined = isMined;
            }

            public void Update(Vector3 position, bool isMined)
            {
                _position = position;
                _isMined = isMined;
            }
        }
    }
}