using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Item;
using Game.Crafting.Interface;

namespace Game.Crafting.Config
{
    public class CraftConfig : ICraftingConfig
    {
        private List<CraftingConfigData> _configDataList = new();
        public CraftConfig(ItemStackFactory itemStackFactory,ConfigJsonList configJson)
        {
            //ロードしたコンフィグのデータを元に、CraftingConfigDataを作成
            var loadedData = new CraftConfigJsonLoad().Load(configJson.SortedCraftRecipeConfigJsonList);
            foreach (var config in loadedData)
            {
                var items = config.Items.Select(item => itemStackFactory.Create(item.Id, item.Count)).ToList();
                var resultItem = itemStackFactory.Create(config.Result.Id, config.Result.Count);
                
                _configDataList.Add(new CraftingConfigData(items,resultItem));
            }
        }

        public IReadOnlyList<CraftingConfigData> GetCraftingConfigList()
        {
            return _configDataList;
        }
    }
}