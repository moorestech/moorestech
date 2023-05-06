using System.Collections.Generic;
using Game.MapObject.Interface.Json;

namespace Game.MapObject.Interface
{
    public interface IMapObjectDatastore
    {

        public void InitializeObject(List<ConfigMapObjectData> jsonMapObjectDataList);
        public void LoadObject(List<SaveMapObjectData> jsonMapObjectDataList);
        
        public void Add(IMapObject mapObject);
        public void Destroy(int id);

        public List<SaveMapObjectData> GetSettingsSaveData();
    }
}