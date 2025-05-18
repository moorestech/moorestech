using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master
{
    public static class MachineRecipeMasterUtil
    {
        public static bool TryGetRecipeElement(
            BlockId blockId,
            IReadOnlyList<IItemStack> inputSlot,
            IReadOnlyList<FluidContainer> fluidInputSlot,
            out MachineRecipeMasterElement recipe
        )
        {
            var itemIds = new List<ItemId>(inputSlot.Count);
            foreach (var inputItem in inputSlot)
            {
                if (inputItem.Id == ItemMaster.EmptyItemId) continue;
                itemIds.Add(inputItem.Id);
            }
            
            return MasterHolder.MachineRecipesMaster.TryGetRecipeElement(blockId, itemIds, out recipe);
        }
        
        public static bool RecipeConfirmation(
            this MachineRecipeMasterElement recipe,
            BlockId blockId,
            IReadOnlyList<IItemStack> inputSlot,
            IReadOnlyList<FluidContainer> fluidInputSlot
        )
        {
            var recipeBlockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            if (recipeBlockId != blockId) return false;
            
            // アイテムが十分な数満たされている数が、必要とする数と一致するか
            var okCnt = 0;
            foreach (var slot in inputSlot)
            {
                if (slot.Id == ItemMaster.EmptyItemId)
                {
                    continue;
                }
                var slotGuid = MasterHolder.ItemMaster.GetItemMaster(slot.Id).ItemGuid;
                okCnt += recipe.InputItems.Count(input => slotGuid == input.ItemGuid && input.Count <= slot.Count);
            }
            
            return okCnt == recipe.InputItems.Length;
        }
    }
}