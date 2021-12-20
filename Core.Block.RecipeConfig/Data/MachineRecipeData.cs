using System.Collections.Generic;
using System.Linq;
using Core.Item;

namespace Core.Block.RecipeConfig.Data
{
    internal class MachineRecipeData : IMachineRecipeData
    {
        public int BlockId { get; }

        public MachineRecipeData(int blockId,int time,List<IItemStack> itemInputs, List<ItemOutput> itemOutputs,int recipeId)
        {
            BlockId = blockId;
            ItemInputs = itemInputs;
            ItemOutputs = itemOutputs;
            RecipeId = recipeId;
            Time = time;
        }

        public List<IItemStack> ItemInputs { get; }

        public List<ItemOutput> ItemOutputs { get; }

        public int Time { get; }
        public int RecipeId { get; }

        public bool RecipeConfirmation(List<IItemStack> inputSlot)
        { 
            int cnt = 0;
            foreach (var slot in inputSlot)
            {
                cnt += ItemInputs.Count(input => slot.Id == input.Id && input.Count <= slot.Count);
            }

            return cnt == ItemInputs.Count;
        }
    }
}