using System.Collections.Generic;
using Core.Item;
using Core.Item.Implementation;

namespace Core.Block.RecipeConfig.Data
{
    public interface IMachineRecipeData
    {
        List<IItemStack> ItemInputs { get; }
        List<ItemOutput> ItemOutputs { get; }
        int BlockId { get; }
        int Time{ get; }
        int RecipeId{ get; }
        bool RecipeConfirmation(List<IItemStack> inputSlot);
    }

    public class ItemOutput
    {
        public IItemStack OutputItem { get; }

        public double Percent { get; }

        public ItemOutput(IItemStack outputItemMachine, double percent)
        {
            OutputItem = outputItemMachine;
            Percent = percent;
        }
    }
}