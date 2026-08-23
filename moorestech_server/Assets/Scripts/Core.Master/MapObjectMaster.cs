using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.MapModule;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MapObjectMaster : IMasterValidator
    {
        public readonly Map Map;

        // アイテム→落とすmapObject索引
        // earn item GUID → mapObjectGuids dropping that item
        private Dictionary<Guid, HashSet<Guid>> _mapObjectGuidsByEarnItem;

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
            MapObjectMasterUtil.Initialize(Map, out _mapObjectGuidsByEarnItem);
        }

        /// <summary>
        ///     そのアイテムをドロップする全マップオブジェクトのGUIDを取得（該当なしなら空）
        ///     Gets the GUIDs of every map object dropping the item (empty when none drops it).
        /// </summary>
        public HashSet<Guid> GetMapObjectGuidsByEarnItem(Guid itemGuid)
        {
            // 索引そのものを渡すと呼び出し側の変更がマスタへ波及するため、常に複製を返す
            // Handing out the index itself would let callers mutate the master, so always return a copy
            if (!_mapObjectGuidsByEarnItem.TryGetValue(itemGuid, out var mapObjectGuids)) return new HashSet<Guid>();

            return new HashSet<Guid>(mapObjectGuids);
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
