using System.Collections.Generic;

namespace Game.Crafting.Interface
{
    public interface ICraftingConfig
    {
        IReadOnlyList<CraftingConfigInfo> CraftingConfigList { get; }
        public IReadOnlyList<CraftingConfigInfo> GetResultItemCraftingConfigList(int itemId);
        public CraftingConfigInfo GetCraftingConfigData(int index);
    }
}