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

                try
                {
                    var itemConfigData = JsonConvert.DeserializeObject<ItemConfigJsonData[]>(json);
                    if (itemConfigData == null)
                    {
                        continue;
                    }
                    
                    
                    var configList = itemConfigData.ToList().Select(c => new ItemConfigData(c, mod, xxHash));
                    itemConfigList.AddRange(configList);
                }
                catch (Exception e)
                {
                    //TODO ログ基盤に入れる
                    Console.WriteLine(e.Message + "\n" + e.StackTrace + "\n アイテムコンフィグのロードに失敗しました mod id:" + mod);
                    continue;
                }
            }
            return itemConfigList;
        }
    }
    
    public class ItemConfigData
    {
        public readonly string ModId;
        public string Id { get; }
        public int MaxStack { get; }
        public readonly ulong ItemHash = 0;

        internal ItemConfigData(ItemConfigJsonData jsonData,string modId,IxxHash xxHash)
        {
            ModId = modId;
            Id = jsonData.Id;
            MaxStack = jsonData.MaxStack;
            ItemHash = 1;
            
            ItemHash = BitConverter.ToUInt64(xxHash.ComputeHash(modId + "/" + Id).Hash);
        }

        /// <summary>
        /// アイテムが定義されていないとき用のコンストラクタ
        /// </summary>
        /// <param name="id"></param>
        /// <param name="maxStack"></param>
        /// <param name="modId"></param>
        public ItemConfigData(string id, int maxStack, string modId)
        {
            Id = id;
            MaxStack = maxStack;
            ModId = modId;
        }
    }

    [JsonObject("SpaceAssets")]
    internal class ItemConfigJsonData
    {
        public string Id => _id;
        public int MaxStack => _maxStack;
        
        [JsonProperty("id")]
        private string _id;
        [JsonProperty("maxStacks")]
        private int _maxStack;

        public ItemConfigJsonData(string id, int maxStack)
        {
            _id = id;
            _maxStack = maxStack;
        }
    }
}