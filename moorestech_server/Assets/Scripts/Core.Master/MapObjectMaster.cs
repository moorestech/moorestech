using System;
using Mooresmaster.Loader.MapObjectsModule;
using Mooresmaster.Model.MapObjectsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MapObjectMaster
    {
        public readonly MapObjects MapObjects;
        
        public MapObjectMaster(JToken jToken)
        {
            MapObjects = MapObjectsLoader.Load(jToken);
        }
        
        public MapObjectElement GetMapObjectElement(Guid guid)
        {
            return Array.Find(MapObjects.Data, x => x.MapObjectGuid == guid);
        }
    }
}