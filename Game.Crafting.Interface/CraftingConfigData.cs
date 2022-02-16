using System.Collections.Generic;
using Core.Item;

namespace Game.Crafting.Interface
{
    public class CraftingConfigData
    {
        public readonly List<IItemStack> Items;
        public readonly IItemStack Result;

        public CraftingConfigData(IItemStack result, List<IItemStack> items)
        {
            Result = result;
            Items = items;
        }
    }
}