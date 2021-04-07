using industrialization.Item;

namespace industrialization.Config
{
    public class NullMachineRecipeData : IMachineRecipeData
    {
        public MacineRecipeInput[] ItemInputs
        {
            get
            {
                return new MacineRecipeInput[0];
            }
        }

        public MacineRecipeOutput[] ItemOutputs { get; }

        public bool RecipeConfirmation(IItemStack[] InputSlot)
        {
            return false;
        }
    }
}