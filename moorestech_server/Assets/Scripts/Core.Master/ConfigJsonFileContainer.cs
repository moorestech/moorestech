using System.Collections.Generic;
using System.Linq;

namespace Core.Master
{
    public class ConfigJsonFileContainer
    {        /// <summary>
        ///     ブロックのコンフィグが入っている
        ///     Key modId : Value ConfigJson
        /// </summary>
        public readonly Dictionary<string, string> BlockConfigs = new();
        
        /// <summary>
        ///     アイテムのコンフィグが入っている
        ///     Key modId : Value ConfigJson
        /// </summary>
        public readonly Dictionary<string, string> ItemConfigs = new();
        
        public readonly List<string> SortedChallengeConfigJsonList = new();
        
        public readonly List<string> SortedCraftRecipeConfigJsonList = new();
        
        public readonly List<string> SortedMachineRecipeConfigJsonList = new();
        
        public readonly List<string> SortedMapObjectConfigJsonList = new();
        public readonly List<string> SortedModIds;
        
        public readonly List<ConfigJson> ConfigJsons;
        
        public ConfigJsonFileContainer(Dictionary<string, ConfigJson> configs)
        {
            ConfigJsons = configs.Values.ToList();
            
            var keys = configs.Keys.ToList();
            keys.Sort();
            SortedModIds = keys;
            
            foreach (var key in keys)
            {
                if (configs[key].ItemConfigJson != string.Empty) ItemConfigs.Add(key, configs[key].ItemConfigJson);
                if (configs[key].BlockConfigJson != string.Empty) BlockConfigs.Add(key, configs[key].BlockConfigJson);
                if (configs[key].CraftRecipeConfigJson != string.Empty) SortedCraftRecipeConfigJsonList.Add(configs[key].CraftRecipeConfigJson);
                if (configs[key].MachineRecipeConfigJson != string.Empty) SortedMachineRecipeConfigJsonList.Add(configs[key].MachineRecipeConfigJson);
                if (configs[key].MapObjectConfigJson != string.Empty) SortedMapObjectConfigJsonList.Add(configs[key].MapObjectConfigJson);
                if (configs[key].ChallengeConfigJson != string.Empty) SortedChallengeConfigJsonList.Add(configs[key].ChallengeConfigJson);
            }
        }
        
    }
}