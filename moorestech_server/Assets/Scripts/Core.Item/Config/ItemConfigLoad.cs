using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Data.HashFunction.xxHash;
using System.Linq;
using Core.Const;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Item.Config
{
    public class ItemConfigLoad
    {
        public List<ItemConfigData> LoadFromJsons(Dictionary<string, string> jsons, List<string> mods)
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
                    ItemConfigJsonData[] itemConfigData = JsonConvert.DeserializeObject<ItemConfigJsonData[]>(json);
                    if (itemConfigData == null) continue;


                    IEnumerable<ItemConfigData> configList = itemConfigData.ToList().Select(c => new ItemConfigData(c, mod, xxHash, itemConfigList.Count + 1));
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

    public class ItemConfigData
    {
        public readonly long ItemHash;
        public readonly int ItemId;
        public readonly string ModId;

        internal ItemConfigData(ItemConfigJsonData jsonData, string modId, IxxHash xxHash, int itemId)
        {
            ModId = modId;
            ItemId = itemId;
            Name = jsonData.Name;
            MaxStack = jsonData.MaxStack;
            ImagePath = jsonData.ImagePath;
            ItemHash = 1;

            ItemHash = BitConverter.ToInt64(xxHash.ComputeHash(modId + "/" + Name).Hash);
        }

        /// <summary>
        ///     アイテムが定義されていないとき用のコンストラクタ
        /// </summary>
        public ItemConfigData(string name, int maxStack, string modId, int itemId)
        {
            Name = name;
            MaxStack = maxStack;
            ModId = modId;
            ItemId = itemId;
        }

        public string Name { get; }
        public int MaxStack { get; }
        public string ImagePath { get; }
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