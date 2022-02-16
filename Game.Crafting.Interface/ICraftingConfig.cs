using System.Collections.Generic;

namespace Game.Crafting.Interface
{
    public interface ICraftingConfig
    {
        public IReadOnlyList<CraftingConfigData> GetCraftingConfig();
    }
}