using System;
using Core.Master.Validator;
using Mooresmaster.Loader.MapObjectsModule;
using Mooresmaster.Model.MapObjectsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MapObjectMaster : IMasterValidator
    {
        public readonly MapObjects MapObjects;

        public MapObjectMaster(JToken jToken)
        {
            MapObjects = MapObjectsLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            return MapObjectMasterUtil.Validate(MapObjects, out errorLogs);
        }

        public void Initialize()
        {
            MapObjectMasterUtil.Initialize(MapObjects);
        }

        public MapObjectMasterElement GetMapObjectElement(Guid guid)
        {
            return Array.Find(MapObjects.Data, x => x.MapObjectGuid == guid);
        }
    }
}