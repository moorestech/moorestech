using System.Collections.Generic;
using Core.Item;

namespace Game.Crafting.Interface
{
    public class CraftingConfigData
    {
        public readonly List<IItemStack> Items;
        public readonly IItemStack Result;

        public CraftingConfigData(List<IItemStack> items,IItemStack result)
        {
            Result = result;
            Items = items;
        }
    }
}