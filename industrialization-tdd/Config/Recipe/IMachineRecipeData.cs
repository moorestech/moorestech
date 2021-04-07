using industrialization.Item;

namespace industrialization.Config
{
    public interface IMachineRecipeData
    {
        MacineRecipeInput[] ItemInputs { get; }
        MacineRecipeOutput[] ItemOutputs { get; }
        bool RecipeConfirmation(IItemStack[] InputSlot);
    }
}