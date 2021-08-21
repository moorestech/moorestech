using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Item;

namespace industrialization.Core.Config.Recipe.Data
{
    public class NullMachineRecipeData : IMachineRecipeData
    {
        public List<IItemStack> ItemInputs => System.Array.Empty<IItemStack>().ToList();
        public List<ItemOutput> ItemOutputs => System.Array.Empty<ItemOutput>().ToList();
        public int BlockId => -1;
        public int Time => 0;

        public bool RecipeConfirmation(List<IItemStack> inputSlot)
        {
            return false;
        }
    }
}