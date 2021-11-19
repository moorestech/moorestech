using System.Collections.Generic;
using Core.Item;

namespace Core.Block.RecipeConfig.Data
{
    public interface IMachineRecipeData
    {
        List<IItemStack> ItemInputs { get; }
        List<ItemOutput> ItemOutputs { get; }
        int BlockId { get; }
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