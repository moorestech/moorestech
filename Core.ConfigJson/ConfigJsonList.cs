using System.Collections.Generic;
using System.Linq;

namespace Core.ConfigJson
{
    public class ConfigJsonList
    {
        /// <summary>
        /// アイテムのコンフィグが入っている
        /// Key modId : Value ConfigJson
        /// </summary>
        public readonly Dictionary<string,string> ItemConfigs = new();
        /// <summary>
        /// ブロックのコンフィグが入っている
        /// Key modId : Value ConfigJson
        /// </summary>
        public readonly Dictionary<string,string> BlockConfigs = new();
        /// <summary>
        /// 鉱石のコンフィグ
        /// Key modId : Value ConfigJson
        /// </summary>
        public readonly Dictionary<string,string> OreConfigs = new();
        /// <summary>
        /// クエストのコンフィグ
        /// Key modId : Value ConfigJson
        /// </summary>
        public readonly Dictionary<string,string> QuestConfigs = new();
        
        public readonly List<string> SortedModIds;

        public readonly List<string> SortedMachineRecipeConfigJsonList = new();
        public readonly List<string> SortedCraftRecipeConfigJsonList = new();

        public ConfigJsonList(Dictionary<string,ConfigJson> configs)
        {
            var keys = configs.Keys.ToList();
            keys.Sort();
            SortedModIds = keys;

            foreach (var key in keys)
            {
                if (configs[key].ItemConfigJson != string.Empty) { ItemConfigs.Add(key,configs[key].ItemConfigJson); }
                if (configs[key].BlockConfigJson != string.Empty) { BlockConfigs.Add(key,configs[key].BlockConfigJson); }
                if (configs[key].MachineRecipeConfigJson != string.Empty) { SortedMachineRecipeConfigJsonList.Add(configs[key].MachineRecipeConfigJson); }
                if (configs[key].CraftRecipeConfigJson != string.Empty) { SortedCraftRecipeConfigJsonList.Add(configs[key].CraftRecipeConfigJson); }
                if (configs[key].OreConfigJson != string.Empty) { OreConfigs.Add(key,configs[key].OreConfigJson); }
                if (configs[key].QuestConfigJson != string.Empty) { QuestConfigs.Add(key,configs[key].QuestConfigJson); }
            }
        }
    }
}