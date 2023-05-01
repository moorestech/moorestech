using System.Collections.Generic;

namespace Game.MapObject.Interface
{
    public interface IMapObjectDatastore
    {
        public void Add(IMapObject mapObject);
        public void Destroy(int id);

        public List<SaveMapObjectData> GetSettingsSaveData();
    }
}