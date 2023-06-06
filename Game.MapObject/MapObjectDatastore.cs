using System;
using System.Collections.Generic;
using System.Linq;
using Game.MapObject.Interface;
using Game.MapObject.Interface.Json;

namespace Game.MapObject
{
    public class MapObjectDatastore : IMapObjectDatastore
    {
        public event Action<IMapObject> OnDestroyMapObject;
        
        /// <summary>
        /// このデータが空になる場合はワールドのロードか初期化ができていない状態である可能性が高いです。
        /// 単純にmapObjectがない場合もありますが、、
        /// <see cref="WorldLoaderFromJson"/>でロードもしくは初期化を行ってください。
        /// </summary>
        private readonly Dictionary<int,IMapObject> _mapObjects = new();
        private readonly IMapObjectFactory _mapObjectFactory;

        public MapObjectDatastore(IMapObjectFactory mapObjectFactory)
        {
            _mapObjectFactory = mapObjectFactory;
        }

        public IReadOnlyList<IMapObject> MapObjects => _mapObjects.Values.ToList();

        public void InitializeObject(List<ConfigMapObjectData> jsonMapObjectDataList)
        {
            foreach (var configMapObject in jsonMapObjectDataList)
            {
                var mapObject = _mapObjectFactory.Create(configMapObject.Type, configMapObject.Position);
                _mapObjects.Add(mapObject.InstanceId, mapObject);
                mapObject.OnDestroy += () => OnDestroyMapObject?.Invoke(mapObject);
            }
        }

        public void LoadObject(List<SaveMapObjectData> jsonMapObjectDataList)
        {
            foreach (var data in jsonMapObjectDataList)
            {
                var mapObject = _mapObjectFactory.Create(data.InstanceId,data.Type, data.Position, data.IsDestroyed);
                _mapObjects.Add(mapObject.InstanceId, mapObject);
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