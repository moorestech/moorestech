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
                SortedItemConfigJsonList.Add(configs[key].ItemConfigJson);
                SortedBlockConfigJsonList.Add(configs[key].BlockConfigJson);
                SortedMachineRecipeConfigJsonList.Add(configs[key].MachineRecipeConfigJson);
                SortedCraftRecipeConfigJsonList.Add(configs[key].CraftRecipeConfigJson);
                SortedCraftRecipeConfigJsonList.Add(configs[key].OreConfigJson);
            }
        }
    }
}