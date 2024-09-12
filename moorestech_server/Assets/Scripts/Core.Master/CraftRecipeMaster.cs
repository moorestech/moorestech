using System;
using Mooresmaster.Loader.CraftRecipesModule;
using Mooresmaster.Model.CraftRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class CraftRecipeMaster
    {
        public readonly CraftRecipes CraftRecipes;
        
        public CraftRecipeMaster(JToken craftRecipeJToken)
        {
            CraftRecipes = CraftRecipesLoader.Load(craftRecipeJToken);
        }
        
        public CraftRecipeMasterElement GetCraftRecipe(Guid guid)
        {
            return Array.Find(CraftRecipes.Data, x => x.CraftRecipeGuid == guid);
        }
    }
}