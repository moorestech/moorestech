using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Item.Interface;
using Game.Crafting.Interface;

namespace Game.Crafting.Config
{
    public class CraftConfig : ICraftingConfig
    {
        private readonly List<CraftingConfigInfo> _configDataList;
        
        public CraftConfig(IItemStackFactory itemStackFactory, ConfigJsonFileContainer configJson)
        {
            //ロードしたコンフィグのデータを元に、CraftingConfigDataを作成
            _configDataList = new CraftConfigJsonLoad(itemStackFactory).Load(configJson.SortedCraftRecipeConfigJsonList);
        }
        
        public IReadOnlyList<CraftingConfigInfo> CraftingConfigList => _configDataList;
        
        public IReadOnlyList<CraftingConfigInfo> GetResultItemCraftingConfigList(int itemId)
        {
            return _configDataList.Where(configData => configData.ResultItem.Id == itemId).ToList();
        }
        
        public CraftingConfigInfo GetCraftingConfigData(int index)
        {
            return _configDataList[index];
        }
    }
}