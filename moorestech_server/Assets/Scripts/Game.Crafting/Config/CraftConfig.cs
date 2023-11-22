using System.Collections.Generic;
using Core.ConfigJson;
using Core.Item;
using Game.Crafting.Interface;

namespace Game.Crafting.Config
{
    public class CraftConfig : ICraftingConfig
    {
        private readonly List<CraftingConfigData> _configDataList;

        public CraftConfig(ItemStackFactory itemStackFactory, ConfigJsonList configJson)
        {
            //ロードしたコンフィグのデータを元に、CraftingConfigDataを作成
            _configDataList =
                new CraftConfigJsonLoad(itemStackFactory).Load(configJson.SortedCraftRecipeConfigJsonList);
        }

        public IReadOnlyList<CraftingConfigData> GetCraftingConfigList()
        {
            return _configDataList;
        }

        public CraftingConfigData GetCraftingConfigData(int index)
        {
            return _configDataList[index];
        }
    }
}