using industrialization.Item;

namespace industrialization.Config
{
    public class NullMachineRecipeData : IMachineRecipeData
    {
        public bool RecipeConfirmation(IItemStack[] InputSlot)
        {
            return false;
        }
    }
}