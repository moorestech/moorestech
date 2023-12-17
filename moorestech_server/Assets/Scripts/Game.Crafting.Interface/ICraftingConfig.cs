using System.Collections.Generic;

namespace Game.Crafting.Interface
{
    public interface ICraftingConfig
    {
        public IReadOnlyList<CraftingConfigData> GetCraftingConfigList();
        public IReadOnlyList<CraftingConfigData> GetResultItemCraftingConfigList(int itemId);
        public CraftingConfigData GetCraftingConfigData(int index);
    }
}