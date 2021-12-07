using System.Collections.Generic;
using Core.Block.RecipeConfig.Data;
using Core.Item;

namespace Core.Block.RecipeConfig
{
    public interface IMachineRecipeConfig
    {
        public IMachineRecipeData GetRecipeData(int BlockId, List<IItemStack> inputItem);
        public IMachineRecipeData GetNullRecipeData();
        public IMachineRecipeData GetRecipeData(int id);
    }
}