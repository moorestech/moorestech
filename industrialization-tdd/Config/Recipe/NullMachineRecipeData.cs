using industrialization.Item;

namespace industrialization.Config
{
    public class NullMachineRecipeData : IMachineRecipeData
    {
        public IItemStack[] ItemInputs { get; }
        public ItemOutput[] ItemOutputs { get; }
        public double Time { get; }

        public bool RecipeConfirmation(IItemStack[] InputSlot)
        {
            return false;
        }
    }
}