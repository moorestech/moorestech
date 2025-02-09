using System;
using System.Collections.Generic;
using Game.Map.Interface.Json;
using UnityEngine;

namespace Game.Map.Interface.MapObject
{
    public interface IMapObjectDatastore
    {
        public IReadOnlyList<IMapObject> MapObjects { get; }
        
        /// <summary>
        ///     オブジェクトをロードするか生成する
        ///     既に存在するオブジェクトはデータを適応し、存在しないオブジェクトは生成する
        /// </summary>
        public void LoadMapObject(List<MapObjectJsonObject> savedMapObjects);
        
        public void Add(IMapObject mapObject);
        public IMapObject Get(int instanceId);
        public List<IMapObject> GetWithinBoundingBox(Vector3 minPosition, Vector3 maxPosition);
        
        public List<MapObjectJsonObject> GetSaveJsonObject();
        
        public event Action<IMapObject> OnDestroyMapObject;
    }
}