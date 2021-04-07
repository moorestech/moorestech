using industrialization.Item;

namespace industrialization.Config.Recipe
{
    public interface IMachineRecipeData
    {
        IItemStack[] ItemInputs { get; }
        ItemOutput[] ItemOutputs { get; }
        double Time{ get; }
        bool RecipeConfirmation(IItemStack[] inputSlot);
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