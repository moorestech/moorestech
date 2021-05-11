using System.Collections.Generic;
using industrialization.Item;

namespace industrialization.Config.Recipe.Data
{
    public interface IMachineRecipeData
    {
        List<IItemStack> ItemInputs { get; }
        ItemOutput[] ItemOutputs { get; }
        int InstallationId { get; }
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