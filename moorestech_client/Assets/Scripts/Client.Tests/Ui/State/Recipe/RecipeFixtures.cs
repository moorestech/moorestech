using System;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;

namespace Client.Tests.Ui.State.Recipe
{
    // Web の craftRecipe()・craft()・machine() の写し
    // Ports of the Web craftRecipe(), craft() and machine() helpers
    public static class RecipeFixtures
    {
        public static Guid RecipeGuid(int number) => new($"88000000-0000-4000-8000-{number:D12}");
        public static ItemAmount Amount(int itemId, int count) => new(new ItemId(itemId), count);

        public static ItemId[] Ids(params int[] ids)
        {
            var result = new ItemId[ids.Length];
            for (var i = 0; i < ids.Length; i++) result[i] = new ItemId(ids[i]);
            return result;
        }

        public static CraftRecipeData Craft(Guid recipeGuid, int resultItemId, int resultCount, double craftTime, params ItemAmount[] requiredItems)
        {
            return new CraftRecipeData(recipeGuid, new ItemId(resultItemId), resultCount, craftTime, requiredItems);
        }

        public static MachineRecipeData Machine(Guid recipeGuid, int outputItemId, int blockId, RecipeLevelInheritance inheritance)
        {
            return new MachineRecipeData(recipeGuid, new BlockId(blockId), 4, new[] { Amount(1, 1) }, new[] { Amount(outputItemId, 1) }, Array.Empty<RecipeFluidAmount>(), Array.Empty<RecipeFluidAmount>(), inheritance);
        }
    }
}
