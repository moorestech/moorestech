using System;
using System.Collections.Generic;
using Core.ConfigJson;

namespace Core.Item.Config
{
    public class ItemConfig : IItemConfig
    {
        private readonly List<ItemConfigData> _itemConfigList;
        private readonly Dictionary<ulong, ItemConfigData> _bockHashToConfig = new();
        private const int DefaultItemMaxCount = int.MaxValue;

        public ItemConfig(ConfigJsonList configPath)
        {
            _itemConfigList = new ItemConfigLoad().LoadFromJsons(configPath.ItemConfigs,configPath.SortedModIds);
            foreach (var itemConfig in _itemConfigList)
            {
                if (_bockHashToConfig.ContainsKey(itemConfig.ItemHash))
                {
                    throw new Exception("ブロック名 " + itemConfig.Name + " は重複しています。");
                }
                
                _bockHashToConfig.Add(itemConfig.ItemHash, itemConfig);
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

        public ItemConfigData GetItemConfig(ulong itemHash)
        {
            if (_bockHashToConfig.TryGetValue(itemHash, out var blockConfig))
            {
                return blockConfig;
            }

            throw new Exception("ItemHash not found:" + itemHash);
        }
    }
}