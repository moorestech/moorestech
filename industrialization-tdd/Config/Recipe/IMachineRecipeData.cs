using industrialization.Item;

namespace industrialization.Config
{
    public interface IMachineRecipeData
    {
        bool RecipeConfirmation(IItemStack[] InputSlot);
    }
}