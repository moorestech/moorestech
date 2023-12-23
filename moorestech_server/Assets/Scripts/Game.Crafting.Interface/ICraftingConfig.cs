using System.Collections.Generic;

namespace Game.Crafting.Interface
{
    public interface ICraftingConfig
    {
        IReadOnlyList<CraftingConfigData> CraftingConfigList { get; }
        public IReadOnlyList<CraftingConfigData> GetResultItemCraftingConfigList(int itemId);
        public CraftingConfigData GetCraftingConfigData(int index);
    }
}