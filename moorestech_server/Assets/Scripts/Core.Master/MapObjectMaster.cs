using System;
using Core.Master.Validator;
using Mooresmaster.Loader.MapModule;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MapObjectMaster : IMasterValidator
    {
        public readonly Map Map;

        public MapObjectMaster(JToken jToken)
        {
            Map = MapLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            return MapObjectMasterUtil.Validate(Map, out errorLogs);
        }

        public void Initialize()
        {
            MapObjectMasterUtil.Initialize(Map);
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
            return Array.Find(Map.MapObjects, x => x.MapObjectGuid == guid);
        }
    }
}
