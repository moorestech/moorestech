﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Map.Interface;
using Game.Map.Interface.Json;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Newtonsoft.Json;

namespace Game.Map
{
    public class MapObjectDatastore : IMapObjectDatastore
    {
        private readonly IMapObjectFactory _mapObjectFactory;

        /// <summary>
        ///     このデータが空になる場合はワールドのロードか初期化ができていない状態である可能性が高いです。
        ///     単純にmapObjectがない場合もありますが、、
        ///     <see cref="WorldLoaderFromJson" />でロードもしくは初期化を行ってください。
        /// </summary>
        private readonly Dictionary<int, IMapObject> _mapObjects = new();

        public MapObjectDatastore(IMapObjectFactory mapObjectFactory, MapInfoJson mapInfoJson)
        {
            _mapObjectFactory = mapObjectFactory;

            //configからmap obejctを生成
            foreach (var configMapObject in mapInfoJson.MapObjects)
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
                if (_mapObjects.TryGetValue(data.instanceId, out var loadedMapObject))
                {
                    if (data.isDestroyed) loadedMapObject.Destroy();
                }
                else
                {
                    var mapObject =
                        _mapObjectFactory.Create(data.instanceId, data.type, data.Position, data.isDestroyed);
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