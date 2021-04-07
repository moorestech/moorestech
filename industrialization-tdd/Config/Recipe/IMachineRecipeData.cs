using industrialization.Item;

namespace industrialization.Config
{
    public interface IMachineRecipeData
    {
        IItemStack[] ItemInputs { get; }
        ItemOutput[] ItemOutputs { get; }
        double Time{ get; }
        bool RecipeConfirmation(IItemStack[] InputSlot);
    }

    public class ItemOutput
    {
        public ItemStack OutputItem { get; }

        public double Percent => percent;

        readonly private double percent;

        public ItemOutput(ItemStack outputItemMacine, double percent)
        {
            OutputItem = outputItemMacine;
            this.percent = percent;
        }
    }
}