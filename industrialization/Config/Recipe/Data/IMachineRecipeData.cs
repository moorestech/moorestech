using industrialization.Item;

namespace industrialization.Config.Recipe.Data
{
    public interface IMachineRecipeData
    {
        IItemStack[] ItemInputs { get; }
        ItemOutput[] ItemOutputs { get; }
        int InstallationId { get; }
        long Time{ get; }
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