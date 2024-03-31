using System.Collections.Generic;
using Server.Core.Item;

namespace Game.Block.Interface.RecipeConfig
{
    public interface IMachineRecipeConfig
    {
        public MachineRecipeData GetRecipeData(int blockId, IReadOnlyList<IItemStack> inputItem);
        public MachineRecipeData GetEmptyRecipeData();
        public MachineRecipeData GetRecipeData(int id);

        public IReadOnlyList<MachineRecipeData> GetAllRecipeData();
    }
}