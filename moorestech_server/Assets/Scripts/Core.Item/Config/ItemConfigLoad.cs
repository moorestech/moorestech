using System;
using System.Collections.Generic;
using System.Data.HashFunction.xxHash;
using System.Linq;
using Core.Const;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Item.Config
{
    public class ItemConfigLoad
    {
        public static List<ItemConfigData> LoadFromJsons(Dictionary<string, string> jsons, List<string> mods)
        {
            var xxHash = xxHashFactory.Instance.Create(new xxHashConfig
            {
                Seed = xxHashConst.DefaultSeed,
                HashSizeInBits = xxHashConst.DefaultSize,
            });
            
            var itemConfigList = new List<ItemConfigData>();
            foreach (var mod in mods)
            {
                if (!jsons.TryGetValue(mod, out var json)) continue;
                
                try
                {
                    var itemConfigData = JsonConvert.DeserializeObject<ItemConfigJsonData[]>(json);
                    if (itemConfigData == null) continue;
                    
                    
                    var configList = itemConfigData.ToList().Select(c => new ItemConfigData(c, mod, xxHash, itemConfigList.Count + 1));
                    itemConfigList.AddRange(configList);
                }
                catch (Exception e)
                {
                    //TODO ログ基盤に入れる
                    Debug.Log(e.Message + "\n" + e.StackTrace + "\n アイテムコンフィグのロードに失敗しました mod id:" + mod);
                }
            }
            
            return itemConfigList;
        }
    }
    
    [JsonObject("SpaceAssets")]
    internal class ItemConfigJsonData
    {
        [JsonProperty("imagePath")] private string _imagePath;
        
        [JsonProperty("maxStacks")] private int _maxStack;
        
        [JsonProperty("name")] private string _name;
        
        public string Name => _name;
        public int MaxStack => _maxStack;
        public string ImagePath => _imagePath;
    }
}