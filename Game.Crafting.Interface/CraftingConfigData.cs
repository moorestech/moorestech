using System.Collections.Generic;
using Core.Item;

namespace Game.Crafting.Interface
{
    public class CraftingConfigData
    {
        public readonly List<CraftingItemData> CraftItemInfos;
        public readonly IItemStack Result;
        
        
        public readonly List<IItemStack> CraftItems;

        public CraftingConfigData(List<CraftingItemData> craftItemInfos,IItemStack result)
        {
            Result = result;
            CraftItemInfos = craftItemInfos;
            CraftItems = new List<IItemStack>();
            foreach (var craftItemInfo in craftItemInfos)
            {
                CraftItems.Add(craftItemInfo.ItemStack);
            }
        }
    }

    public class CraftingItemData
    {
        public readonly IItemStack ItemStack;
        public readonly bool IsRemain;

        public CraftingItemData(IItemStack itemStack, bool isRemain)
        {
            ItemStack = itemStack;
            IsRemain = isRemain;
        }
    }
}