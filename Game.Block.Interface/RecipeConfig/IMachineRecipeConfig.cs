using System.Collections.Generic;
using Core.Block.RecipeConfig.Data;
using Core.Item;

namespace Core.Block.RecipeConfig
{
    public interface IMachineRecipeConfig
    {
        public MachineRecipeData GetRecipeData(int BlockId, IReadOnlyList<IItemStack> inputItem);
        public MachineRecipeData GetEmptyRecipeData();
        public MachineRecipeData GetRecipeData(int id);
        
        public IReadOnlyList<MachineRecipeData> GetAllRecipeData();
    }
}