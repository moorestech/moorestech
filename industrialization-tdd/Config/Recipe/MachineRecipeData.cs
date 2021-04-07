using industrialization.Item;

namespace industrialization.Config
{
    public class MachineRecipeData : IMachineRecipeData
    {
        public int InstallationId => installationId;
        private readonly int installationId;

        public MachineRecipeData(int InstallationId,double time,ItemStack[] itemInputs, ItemOutput[] itemOutputs)
        {
            this.installationId = InstallationId;
            this.itemInputs = itemInputs;
            this.itemOutputs = itemOutputs;
            this.time = time;
        }

        public IItemStack[] ItemInputs => itemInputs;
        private readonly IItemStack[] itemInputs;
        public ItemOutput[] ItemOutputs => itemOutputs;
        private readonly ItemOutput[] itemOutputs;
        
        public double Time => time;
        private readonly double time;
        public bool RecipeConfirmation(IItemStack[] InputSlot)
        {
            throw new System.NotImplementedException();
        }
    }
}