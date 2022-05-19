using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Data.HashFunction.xxHash;
using System.Linq;
using System.Runtime.Serialization.Json;
using Core.Config.Item;
using Core.Const;
using Newtonsoft.Json;

namespace Core.Item.Config
{
    public class ItemConfigLoad
    {
        public List<ItemConfigData> LoadFromJsons(Dictionary<string,string> jsons,List<string> mods)
        {
            var xxHash = xxHashFactory.Instance.Create(new xxHashConfig()
            {
                Seed = xxHashConst.DefaultSeed,
                HashSizeInBits = xxHashConst.DefaultSize
            });
            
            var itemConfigList = new List<ItemConfigData>();
            foreach (var mod in mods)
            {
                if (!jsons.TryGetValue(mod,out var json))
                {
                    continue;
                }
                
                var itemConfigData = JsonConvert.DeserializeObject<ItemConfigJsonData[]>(json);
                if (itemConfigData == null)
                {
                    continue;
                }

                var configList = itemConfigData.ToList().Select(c => new ItemConfigData(c, mod, xxHash));
                itemConfigList.AddRange(configList);
            }
            return itemConfigList;
        }
    }
    
    public class ItemConfigData
    {
        public string Name { get; }
        public int MaxStack { get; }
        public readonly ulong ItemHash = 0;

        internal ItemConfigData(ItemConfigJsonData jsonData,string modId,IxxHash xxHash)
        {
            Name = jsonData.Name;
            MaxStack = jsonData.MaxStack;
            ItemHash = 1;
            
            ItemHash = BitConverter.ToUInt64(xxHash.ComputeHash(modId + "/" + Name).Hash);
        }

        public ItemConfigData(string name, int maxStack)
        {
            Name = name;
            MaxStack = maxStack;
        }
    }

    [JsonObject("SpaceAssets")]
    internal class ItemConfigJsonData
    {
        public string Name => _name;
        public int MaxStack => _maxStack;
        
        [JsonProperty("name")]
        private string _name;
        [JsonProperty("max_stacks")]
        private int _maxStack;

        public ItemConfigJsonData(string name, int id, int maxStack)
        {
            _name = name;
            _maxStack = maxStack;
        }
    }
}