using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Data.HashFunction.xxHash;
using System.Linq;
using Core.Const;
using Newtonsoft.Json;

namespace Core.Item.Config
{
    public class ItemConfigLoad
    {
        public List<ItemConfigData> LoadFromJsons(Dictionary<string, string> jsons, List<string> mods)
        {
            var xxHash = xxHashFactory.Instance.Create(new xxHashConfig
            {
                Seed = xxHashConst.DefaultSeed,
                HashSizeInBits = xxHashConst.DefaultSize
            });

            var itemConfigList = new List<ItemConfigData>();
            foreach (var mod in mods)
            {
                if (!jsons.TryGetValue(mod, out var json)) continue;

                try
                {
                    var itemConfigData = JsonConvert.DeserializeObject<ItemConfigJsonData[]>(json);
                    if (itemConfigData == null) continue;


                    var configList = itemConfigData.ToList().Select(c => new ItemConfigData(c, mod, xxHash));
                    itemConfigList.AddRange(configList);
                }
                catch (Exception e)
                {
                    //TODO ログ基盤に入れる
                    Console.WriteLine(e.Message + "\n" + e.StackTrace + "\n アイテムコンフィグのロードに失敗しました mod id:" + mod);
                }
            }

            return itemConfigList;
        }
    }

    public class ItemConfigData
    {
        public readonly ulong ItemHash;
        public readonly string ModId;

        internal ItemConfigData(ItemConfigJsonData jsonData, string modId, IxxHash xxHash)
        {
            ModId = modId;
            Name = jsonData.Name;
            MaxStack = jsonData.MaxStack;
            ImagePath = jsonData.ImagePath;
            ItemHash = 1;

            ItemHash = BitConverter.ToUInt64(xxHash.ComputeHash(modId + "/" + Name).Hash);
        }

        /// <summary>
        ///     アイテムが定義されていないとき用のコンストラクタ
        /// </summary>
        /// <param name="name"></param>
        /// <param name="maxStack"></param>
        /// <param name="modId"></param>
        public ItemConfigData(string name, int maxStack, string modId)
        {
            Name = name;
            MaxStack = maxStack;
            ModId = modId;
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