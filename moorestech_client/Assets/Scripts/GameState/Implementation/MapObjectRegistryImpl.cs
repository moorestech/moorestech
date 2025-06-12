using System.Collections.Generic;
using UnityEngine;

namespace GameState.Implementation
{
    public class MapObjectRegistryImpl : IMapObjectRegistry
    {
        private readonly Dictionary<int, ReadOnlyMapObjectImpl> _mapObjects = new();

        public MapObjectRegistryImpl()
        {
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