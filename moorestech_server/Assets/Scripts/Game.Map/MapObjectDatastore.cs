﻿using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Map.Interface;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.SaveLoad.Json;

namespace Game.Map
{
    public class MapObjectDatastore : IMapObjectDatastore
    {
        public event Action<IMapObject> OnDestroyMapObject;

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

            foreach (var mapObjectInfo in mapInfoJson.MapObjects)
            {
                var mapObjectConfig = ServerContext.MapObjectConfig.GetConfig(mapObjectInfo.Type);
                var hp = mapObjectConfig.Hp;

                var mapObject = _mapObjectFactory.Create(mapObjectInfo.InstanceId, mapObjectInfo.Type, hp, false, mapObjectInfo.Position);
                _mapObjects.Add(mapObject.InstanceId, mapObject);
                mapObject.OnDestroy += () => OnDestroyMapObject?.Invoke(mapObject);
            }
        }

        public IReadOnlyList<IMapObject> MapObjects => _mapObjects.Values.ToList();

        public void LoadMapObject(List<MapObjectJsonObject> savedMapObjects)
        {
            foreach (var savedMapObject in savedMapObjects)
            {
                if (!_mapObjects.TryGetValue(savedMapObject.instanceId, out var loadedMapObject))
                {
                    throw new KeyNotFoundException($"セーブデータ内にあるインスタンスID: {savedMapObject.instanceId} のmapObjectが実際のマップに存在しません。");
                }

                //破壊状況をロード
                if (savedMapObject.isDestroyed) loadedMapObject.Destroy();
                if (savedMapObject.hp != loadedMapObject.CurrentHp) loadedMapObject.Attack(loadedMapObject.CurrentHp - savedMapObject.hp);
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

        public List<MapObjectJsonObject> GetSaveData()
        {
            return _mapObjects.Select(m => new MapObjectJsonObject(m.Value)).ToList();
        }
    }
}