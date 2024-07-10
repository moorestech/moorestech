using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item.Interface;

namespace Game.Block.Interface.RecipeConfig
{
    public class MachineRecipeData
    {
        public MachineRecipeData(int blockId, float time, List<IItemStack> itemInputs, List<ItemOutput> itemOutputs,
            int recipeId)
        {
            BlockId = blockId;
            ItemInputs = itemInputs;
            ItemOutputs = itemOutputs;
            RecipeId = recipeId;
            Time = time;
        }
        
        public int BlockId { get; }
        
        public List<IItemStack> ItemInputs { get; }
        
        public List<ItemOutput> ItemOutputs { get; }
        
        public float Time { get; }
        public int RecipeId { get; }
        
        public static MachineRecipeData CreateEmptyRecipe()
        {
            return new MachineRecipeData(BlockConst.EmptyBlockId, 0, new List<IItemStack>(), new List<ItemOutput>(),
                -1);
        }
        
        public bool RecipeConfirmation(IReadOnlyList<IItemStack> inputSlot, int blockId)
        {
            if (blockId != BlockId) return false;
            
            var cnt = 0;
            foreach (var slot in inputSlot)
                cnt += ItemInputs.Count(input => slot.Id == input.Id && input.Count <= slot.Count);
            
            return cnt == ItemInputs.Count;
        }
    }
}