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

        /// <summary>
        /// マップオブジェクトGUIDからマスターデータを取得（見つからない場合は例外）
        /// Gets the master data from the map object GUID (throws if not found).
        /// </summary>
        public MapObjectMasterElement GetMapObjectElement(Guid guid)
        {
            var result = GetMapObjectElementOrNull(guid);
            if (result == null)
            {
                throw new InvalidOperationException($"MapObjectElement not found. MapObjectGuid:{guid}");
            }
            return result;
        }

        /// <summary>
        /// マップオブジェクトGUIDからマスターデータを取得（見つからない場合はnull）
        /// Gets the master data from the map object GUID (returns null if not found).
        /// </summary>
        public MapObjectMasterElement GetMapObjectElementOrNull(Guid guid)
        {
            return Array.Find(MapObjects.Data, x => x.MapObjectGuid == guid);
        }
    }
}