using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Crafting.Interface
{
    public class CraftingConfigInfo
    {
        public readonly List<CraftingItemData> CraftItemInfos;

        public readonly List<IItemStack> CraftItems;
        public readonly int RecipeId;
        public readonly IItemStack ResultItem;

        public CraftingConfigInfo(List<CraftingItemData> craftItemInfos, IItemStack resultItem, int recipeId)
        {
            ResultItem = resultItem;
            RecipeId = recipeId;
            CraftItemInfos = craftItemInfos;
            CraftItems = new List<IItemStack>();
            foreach (var craftItemInfo in craftItemInfos) CraftItems.Add(craftItemInfo.ItemStack);
        }
    }

    public class CraftingItemData
    {
        public readonly bool IsRemain;
        public readonly IItemStack ItemStack;

        public CraftingItemData(IItemStack itemStack, bool isRemain)
        {
            ItemStack = itemStack;
            IsRemain = isRemain;
        }
    }
}