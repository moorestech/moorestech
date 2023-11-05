using System.Collections.Generic;
using Core.Item;
using MainGame.Basic;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class ViewerRecipeData
    {
        public readonly List<ItemStack> ItemStacks = new();
        public readonly int MachineItemId;
        public readonly ViewerRecipeType RecipeType;
        public readonly List<ItemStack> ResultItem = new();

        public ViewerRecipeData(List<IItemStack> itemStacks, List<IItemStack> resultItem, ViewerRecipeType recipeType, int machineItemId)
        {
            foreach (var item in itemStacks) ItemStacks.Add(new ItemStack(item.Id, item.Count));
            foreach (var item in resultItem) ResultItem.Add(new ItemStack(item.Id, item.Count));

            MachineItemId = machineItemId;
            RecipeType = recipeType;
        }

        public ViewerRecipeData(List<IItemStack> itemStacks, IItemStack resultItem, ViewerRecipeType recipeType)
        {
            foreach (var item in itemStacks) ItemStacks.Add(new ItemStack(item.Id, item.Count));
            ResultItem.Add(new ItemStack(resultItem.Id, resultItem.Count));

            RecipeType = recipeType;
        }
    }

    public enum ViewerRecipeType
    {
        Craft,
        Machine
    }
}