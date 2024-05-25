using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Crafting.Interface
{
    public class CraftingConfigInfo
    {
        public readonly List<CraftRequiredItemInfo> CraftRequireItemInfos;

        public readonly int RecipeId;
        public readonly IItemStack ResultItem;

        public CraftingConfigInfo(List<CraftRequiredItemInfo> craftRequireItemInfos, IItemStack resultItem, int recipeId)
        {
            ResultItem = resultItem;
            RecipeId = recipeId;
            CraftRequireItemInfos = craftRequireItemInfos;
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