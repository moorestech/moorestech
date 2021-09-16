using System.Collections.Generic;
using System.Linq;
using Core.Block;
using Core.Item;

namespace Core.Config.Recipe.Data
{
    public class NullMachineRecipeData : IMachineRecipeData
    {
        public List<IItemStack> ItemInputs => System.Array.Empty<IItemStack>().ToList();
        public List<ItemOutput> ItemOutputs => System.Array.Empty<ItemOutput>().ToList();
        public int BlockId => BlockConst.NullBlockId;
        public int Time => 0;

        public bool RecipeConfirmation(List<IItemStack> inputSlot)
        {
            return false;
        }
    }
}