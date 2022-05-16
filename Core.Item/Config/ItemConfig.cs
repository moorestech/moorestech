using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Core.Config.Item;
using Core.ConfigJson;
using Newtonsoft.Json;
using static System.Int32;

namespace Core.Item.Config
{
    public class ItemConfig : IItemConfig
    {
        private readonly List<ItemConfigData> _itemConfigList;
        private const int DefaultItemMaxCount = int.MaxValue;

        public ItemConfig(ConfigJsonList configPath)
        {
            _itemConfigList = new ItemConfigLoad().LoadFromJsons(configPath.SortedItemConfigJsonList);
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

            return new ItemConfigData("undefined id " + id, id, DefaultItemMaxCount);
        }
    }

    [JsonObject("SpaceAssets")]
    public class ItemConfigData
    {
        public string Name => _name;
        public int MaxStack => _maxStack;
        
        [JsonProperty("name")]
        private string _name;
        [JsonProperty("max_stacks")]
        private int _maxStack;

        public ItemConfigData(string name, int id, int maxStack)
        {
            _name = name;
            _maxStack = maxStack;
        }
    }
}