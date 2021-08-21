using System.Collections.Generic;
using industrialization.Core.Item;

namespace industrialization.Core.Config.Recipe.Data
{
    public interface IMachineRecipeData
    {
        List<IItemStack> ItemInputs { get; }
        List<ItemOutput> ItemOutputs { get; }
        uint BlockId { get; }
        int Time{ get; }
        bool RecipeConfirmation(List<IItemStack> inputSlot);
    }

    public class ItemOutput
    {
        public ItemStack OutputItem { get; }

        public double Percent { get; }

        public ItemOutput(ItemStack outputItemMachine, double percent)
        {
            OutputItem = outputItemMachine;
            Percent = percent;
        }
    }
}