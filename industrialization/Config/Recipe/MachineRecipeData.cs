using industrialization.Config.Recipe;
using industrialization.Item;

namespace industrialization.Config
{
    public class MachineRecipeData : IMachineRecipeData
    {
        public int InstallationId { get; }

        public MachineRecipeData(int installationId,double time,IItemStack[] itemInputs, ItemOutput[] itemOutputs)
        {
            InstallationId = installationId;
            ItemInputs = itemInputs;
            ItemOutputs = itemOutputs;
            Time = time;
        }

        public IItemStack[] ItemInputs { get; }

        public ItemOutput[] ItemOutputs { get; }

        public double Time { get; }

        public bool RecipeConfirmation(IItemStack[] inputSlot)
        {
            throw new System.NotImplementedException();
        }
    }
}