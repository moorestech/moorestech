using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Crafting.Interface
{
    public class CraftingConfigInfo
    {
        public readonly List<CraftRequiredItemInfo> CraftRequiredItemInfos;

        public readonly int RecipeId;
        public readonly IItemStack ResultItem;

        public CraftingConfigInfo(List<CraftRequiredItemInfo> craftRequiredItemInfos, IItemStack resultItem, int recipeId)
        {
            ResultItem = resultItem;
            RecipeId = recipeId;
            CraftRequiredItemInfos = craftRequiredItemInfos;
        }
    }

    public class CraftRequiredItemInfo
    {
        public readonly bool IsRemain;
        public readonly IItemStack ItemStack;

        public CraftRequiredItemInfo(IItemStack itemStack, bool isRemain)
        {
            ItemStack = itemStack;
            IsRemain = isRemain;
        }
    }
}