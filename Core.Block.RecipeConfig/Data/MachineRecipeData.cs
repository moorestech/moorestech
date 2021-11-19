using System.Collections.Generic;
using System.Linq;
using Core.Item;

namespace Core.Block.RecipeConfig.Data
{
    public class MachineRecipeData : IMachineRecipeData
    {
        public int BlockId { get; }

        public MachineRecipeData(int blockId,int time,List<IItemStack> itemInputs, List<ItemOutput> itemOutputs)
        {
            BlockId = blockId;
            ItemInputs = itemInputs;
            ItemOutputs = itemOutputs;
            Time = time;
        }

        public List<IItemStack> ItemInputs { get; }

        public List<ItemOutput> ItemOutputs { get; }

        public int Time { get; }

        public bool RecipeConfirmation(List<IItemStack> inputSlot)
        { 
            int cnt = 0;
            foreach (var slot in inputSlot)
            {
                cnt += ItemInputs.Count(input => slot.Id == input.Id && input.Amount <= slot.Amount);
            }

            return cnt == ItemInputs.Count;
        }
    }
}