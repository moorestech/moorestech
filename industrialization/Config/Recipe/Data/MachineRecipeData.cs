using industrialization.Item;

namespace industrialization.Config.Recipe.Data
{
    public class MachineRecipeData : IMachineRecipeData
    {
        public int InstallationId { get; }

        public MachineRecipeData(int installationId,int time,ItemStack[] itemInputs, ItemOutput[] itemOutputs)
        {
            InstallationId = installationId;
            ItemInputs = itemInputs;
            ItemOutputs = itemOutputs;
            Time = time;
        }

        public IItemStack[] ItemInputs { get; }

        public ItemOutput[] ItemOutputs { get; }

        public int Time { get; }

        public bool RecipeConfirmation(IItemStack[] inputSlot)
        {
            throw new System.NotImplementedException();
        }
    }
}