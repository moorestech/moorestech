using System;
using System.Linq;
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
            int cnt = 0;
            foreach (var slot in inputSlot)
            {
                cnt += ItemInputs.Count(input => slot.Id == input.Id && input.Amount <= slot.Amount);
            }

            return cnt == inputSlot.Length;
        }
    }
}