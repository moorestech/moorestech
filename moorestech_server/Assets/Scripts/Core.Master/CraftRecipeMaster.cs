using System;
using Mooresmaster.Model.CraftRecipesModule;

namespace Core.Master
{
    public class CraftRecipeMaster
    {
        public static CraftRecipeElement GetCraftRecipe(Guid guid)
        {
            return Array.Find(MasterHolder.CraftRecipes.Data, x => x.CraftRecipeGuid == guid);
        }
    }
}