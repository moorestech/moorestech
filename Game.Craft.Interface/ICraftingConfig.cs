using System.Collections.Generic;

namespace Game.Craft.Interface
{
    public interface ICraftingConfig
    {
        public IReadOnlyList<CraftingConfigData> GetCraftingConfigList();
    }
}