using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.MapObject.Interface;
using Game.MapObject.Interface.Json;
using Game.Save.Interface;
using Newtonsoft.Json;

namespace Game.MapObject
{
    public class MapObjectDatastore : IMapObjectDatastore
    {
        private readonly IMapObjectFactory _mapObjectFactory;


        ///     。
        ///     mapObject
        ///     <see cref="WorldLoaderFromJson" />。

        private readonly Dictionary<int, IMapObject> _mapObjects = new();

        public MapObjectDatastore(IMapObjectFactory mapObjectFactory, MapConfigFile mapConfigFile)
        {
            _mapObjectFactory = mapObjectFactory;

            //configmap obejct
            var mapObjects = JsonConvert.DeserializeObject<ConfigMapObjects>(File.ReadAllText(mapConfigFile.FullMapObjectConfigFilePath));
            foreach (var configMapObject in mapObjects.MapObjects)
            {
                var mapObject = _mapObjectFactory.Create(configMapObject.InstanceId, configMapObject.Type, configMapObject.Position, false);
                _mapObjects.Add(mapObject.InstanceId, mapObject);
                mapObject.OnDestroy += () => OnDestroyMapObject?.Invoke(mapObject);
            }
        }

        public event Action<IMapObject> OnDestroyMapObject;

        public IReadOnlyList<IMapObject> MapObjects => _mapObjects.Values.ToList();

        public void LoadAndCreateObject(List<SaveMapObjectData> jsonMapObjectDataList)
        {
            foreach (var data in jsonMapObjectDataList)
                if (_mapObjects.TryGetValue(data.InstanceId, out var loadedMapObject))
                {
                    if (data.IsDestroyed) loadedMapObject.Destroy();
                }
                else
                {
                    var mapObject = _mapObjectFactory.Create(data.InstanceId, data.Type, data.Position, data.IsDestroyed);
                    mapObject.OnDestroy += () => OnDestroyMapObject?.Invoke(mapObject);
                }
        }

        public void Add(IMapObject mapObject)
        {
            _mapObjects.Add(mapObject.InstanceId, mapObject);
            mapObject.OnDestroy += () => OnDestroyMapObject?.Invoke(mapObject);
        }

        public IMapObject Get(int instanceId)
        {
            return _mapObjects[instanceId];
        }

        public List<SaveMapObjectData> GetSettingsSaveData()
        {
            return _mapObjects.Select(m => new SaveMapObjectData(m.Value)).ToList();
        }
    }
}