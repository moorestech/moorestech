using System.Collections.Generic;

namespace Game.Crafting.Interface
{
    public interface ICraftingConfig
    {
        public IReadOnlyList<CraftingConfigData> GetCraftingConfigList();
        public CraftingConfigData GetCraftingConfigData(int index);
    }
}