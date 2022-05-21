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
        private const int DefaultItemMaxCount = int.MaxValue;

        public ItemConfig(ConfigJsonList configPath)
        {
            _itemConfigList = new ItemConfigLoad().LoadFromJsons(configPath.ItemConfigs,configPath.SortedModIds);

            
            //実際のIDは1から（空IDの次の値）始まる
            for (int i = ItemConst.EmptyItemId + 1; i <= _itemConfigList.Count; i++)
            {
                var arrayIndex = i - 1;
                if (_bockHashToId.ContainsKey(_itemConfigList[arrayIndex].ItemHash))
                {
                    throw new Exception("アイテム名 " + _itemConfigList[arrayIndex].Name + " は重複しています。");
                }
                _bockHashToId.Add(_itemConfigList[arrayIndex].ItemHash, i);
            }
        }

        public ItemConfigData GetItemConfig(int id)
        {
            //0は何も持っていないことを表すので1から始める
            id -= 1;
            if (id < 0)
            {
                throw new ArgumentException("id must be greater than 0 ID:" + id);
            }
            if (id < _itemConfigList.Count)
            {
                return _itemConfigList[id];
            }

            return new ItemConfigData("undefined id " + id, DefaultItemMaxCount);
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
    }
}