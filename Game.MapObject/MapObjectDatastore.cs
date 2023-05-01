using System.Collections.Generic;
using Game.MapObject.Interface;

namespace Game.MapObject
{
    public class MapObjectDatastore : IMapObjectDatastore
    {
        private readonly Dictionary<int,IMapObject> _mapObjects = new();
        public void Add(IMapObject mapObject)
        {
            _mapObjects.Add(mapObject.Id, mapObject);
        }

        public void Destroy(int id)
        {
            _mapObjects[id].Destroy();
        }
    }
}