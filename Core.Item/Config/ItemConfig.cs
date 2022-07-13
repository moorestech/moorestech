using System;
using System.Collections.Generic;
using Core.ConfigJson;
using Core.Const;

namespace Core.Item.Config
{
    public class ItemConfig : IItemConfig
    {
        private readonly List<ItemConfigData> _itemConfigList;
        private readonly Dictionary<ulong, int> _bockHashToId = new();
        private readonly Dictionary<string,List<int>> _modIdToItemIds = new();

        private const int DefaultItemMaxCount = int.MaxValue;

        public ItemConfig(ConfigJsonList configPath)
        {
            _itemConfigList = new ItemConfigLoad().LoadFromJsons(configPath.ItemConfigs,configPath.SortedModIds);

            
            //実際のIDは1から（空IDの次の値）始まる
            for (int itemId = ItemConst.EmptyItemId + 1; itemId <= _itemConfigList.Count; itemId++)
            {
                var arrayIndex = itemId - 1;
                if (_bockHashToId.ContainsKey(_itemConfigList[arrayIndex].ItemHash))
                {
                    //TODO ログ基盤に入れる
                    throw new Exception("アイテム名 " + _itemConfigList[arrayIndex].Name + " は重複しています。");
                }
                _bockHashToId.Add(_itemConfigList[arrayIndex].ItemHash, itemId);

                if (_modIdToItemIds.TryGetValue(_itemConfigList[arrayIndex].ModId, out var itemIds))
                {
                    itemIds.Add(itemId);
                }
                else
                {
                    _modIdToItemIds.Add(_itemConfigList[arrayIndex].ModId, new List<int> {itemId});
                }
            }
        }

        public ItemConfigData GetItemConfig(int id)
        {
            //0は何も持っていないことを表すので-1してListのindexにする
            id -= 1;
            if (id < 0)
            {
                //TODO ログ基盤に入れる
                throw new ArgumentException("id must be greater than 0 ID:" + id);
            }
            if (id < _itemConfigList.Count)
            {
                return _itemConfigList[id];
            }

            //TODO ログ基盤に入れる
            return new ItemConfigData("undefined id " + id, DefaultItemMaxCount,"mod is not found");
        }

        public int GetItemId(ulong itemHash)
        {
            
            if (_bockHashToId.TryGetValue(itemHash, out var id))
            {
                return id;
            }
            Console.WriteLine("itemHash:" + itemHash + " is not found");
            return ItemConst.EmptyItemId;
        }

        public List<int> GetItemIds(string modId)
        {
            return _modIdToItemIds.TryGetValue(modId, out var itemIds) ? itemIds : new List<int>();
        }
    }
}