using System.Collections.Generic;
using System.Linq;

namespace Core.ConfigJson
{
    public class ConfigJsonList
    {
        public readonly List<string> SortedItemConfigJsonList = new();
        public readonly List<string> SortedBlockConfigJsonList = new();
        public readonly List<string> SortedMachineRecipeConfigJsonList = new();
        public readonly List<string> SortedCraftRecipeConfigJsonList = new();
        public readonly List<string> SortedOreConfigJsonList = new();

        public ConfigJsonList(Dictionary<string,ConfigJson> configs)
        {
            var keys = configs.Keys.ToList();
            keys.Sort();

            foreach (var key in keys)
            {
                if (configs[key].ItemConfigJson != string.Empty) { SortedItemConfigJsonList.Add(configs[key].ItemConfigJson); }
                if (configs[key].BlockConfigJson != string.Empty) { SortedBlockConfigJsonList.Add(configs[key].BlockConfigJson); }
                if (configs[key].MachineRecipeConfigJson != string.Empty) { SortedMachineRecipeConfigJsonList.Add(configs[key].MachineRecipeConfigJson); }
                if (configs[key].CraftRecipeConfigJson != string.Empty) { SortedCraftRecipeConfigJsonList.Add(configs[key].CraftRecipeConfigJson); }
                if (configs[key].OreConfigJson != string.Empty) { SortedOreConfigJsonList.Add(configs[key].OreConfigJson); }
                
            }
        }
    }
}