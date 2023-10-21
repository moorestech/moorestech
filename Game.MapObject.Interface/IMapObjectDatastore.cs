using System;
using System.Collections.Generic;
using Game.MapObject.Interface.Json;

namespace Game.MapObject.Interface
{
    public interface IMapObjectDatastore
    {
        public IReadOnlyList<IMapObject> MapObjects { get; }


        ///     
        ///     

        public void LoadAndCreateObject(List<SaveMapObjectData> jsonMapObjectDataList);

        public void Add(IMapObject mapObject);
        public IMapObject Get(int instanceId);

        public List<SaveMapObjectData> GetSettingsSaveData();

        public event Action<IMapObject> OnDestroyMapObject;
    }
}