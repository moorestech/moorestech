﻿using System;
using System.Collections.Generic;
using Game.Map.Interface.Json;

namespace Game.Map.Interface
{
    public interface IMapObjectDatastore
    {
        public IReadOnlyList<IMapObject> MapObjects { get; }

        /// <summary>
        ///     オブジェクトをロードするか生成する
        ///     既に存在するオブジェクトはデータを適応し、存在しないオブジェクトは生成する
        /// </summary>
        public void LoadAndCreateObject(List<SaveMapObjectData> jsonMapObjectDataList);

        public void Add(IMapObject mapObject);
        public IMapObject Get(int instanceId);

        public List<SaveMapObjectData> GetSettingsSaveData();

        public event Action<IMapObject> OnDestroyMapObject;
    }
}