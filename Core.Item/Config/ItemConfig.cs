using System;
using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Const;

namespace Core.Item.Config
{
    public class ItemConfig : IItemConfig
    {
        private const int DefaultItemMaxCount = int.MaxValue;
        private readonly Dictionary<ulong, int> _bockHashToId = new();
        private readonly List<ItemConfigData> _itemConfigList;
        private readonly Dictionary<string, List<int>> _modIdToItemIds = new();

        public ItemConfig(ConfigJsonList configPath)
        {
            _itemConfigList = new ItemConfigLoad().LoadFromJsons(configPath.ItemConfigs, configPath.SortedModIds);


            //ID1（ID）
            for (var itemId = ItemConst.EmptyItemId + 1; itemId <= _itemConfigList.Count; itemId++)
            {
                var arrayIndex = itemId - 1;
                if (_bockHashToId.ContainsKey(_itemConfigList[arrayIndex].ItemHash))
                    //TODO 
                    throw new Exception(" " + _itemConfigList[arrayIndex].Name + " 。");
                _bockHashToId.Add(_itemConfigList[arrayIndex].ItemHash, itemId);

                if (_modIdToItemIds.TryGetValue(_itemConfigList[arrayIndex].ModId, out var itemIds))
                    itemIds.Add(itemId);
                else
                    _modIdToItemIds.Add(_itemConfigList[arrayIndex].ModId, new List<int> { itemId });
            }
        }

        public ItemConfigData GetItemConfig(int id)
        {
            //0-1Listindex
            id -= 1;
            if (id < 0)
                //TODO 
                throw new ArgumentException("id must be greater than 0 ID:" + id);
            if (id < _itemConfigList.Count) return _itemConfigList[id];

            //TODO 
            return new ItemConfigData("undefined id " + id, DefaultItemMaxCount, "mod is not found");
        }

        public ItemConfigData GetItemConfig(ulong itemHash)
        {
            return GetItemConfig(GetItemId(itemHash));
        }

        public int GetItemId(ulong itemHash)
        {
            if (_bockHashToId.TryGetValue(itemHash, out var id)) return id;
            //TODO 
            Console.WriteLine("itemHash:" + itemHash + " is not found");
            return ItemConst.EmptyItemId;
        }

        public List<int> GetItemIds(string modId)
        {
            if (modId == null) throw new ArgumentException("Mod id is null");
            return _modIdToItemIds.TryGetValue(modId, out var itemIds) ? itemIds : new List<int>();
        }


        public int GetItemId(string modId, string itemName, string callerMethodName = "")
        {
            foreach (var itemId in GetItemIds(modId).Where(i => GetItemConfig(i).Name == itemName)) return itemId;
            //TODO 
            Console.WriteLine($"itemName:{itemName} itemModId:{modId} is not found callerMethodName:{callerMethodName}");
            return ItemConst.EmptyItemId;
        }
    }
}