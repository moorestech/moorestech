using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
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
            var fluidIds = new List<FluidId>(fluidInputSlot.Count);
            foreach (var fluidContainer in fluidInputSlot)
            {
                if (fluidContainer.FluidId == FluidMaster.EmptyFluidId) continue;
                fluidIds.Add(fluidContainer.FluidId);
            }
            
            var found = MasterHolder.MachineRecipesMaster.TryGetRecipeElement(blockId, itemIds, fluidIds, out recipe);

            // アンロックされていないレシピは使用不可
            // Locked recipes cannot be used
            if (found && ServerContext.IsInitialized)
            {
                var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
                if (unlockState != null &&
                    unlockState.MachineRecipeUnlockStateInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) &&
                    !info.IsUnlocked)
                {
                    recipe = null;
                    return false;
                }
            }

            return found;
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
            
            if (okCnt != recipe.InputItems.Length) return false;
            
            // 液体が十分な数満たされているかチェック
            foreach (var inputFluid in recipe.InputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                var found = false;
                
                // 任意のスロットに必要な液体があるかチェック
                foreach (var fluidContainer in fluidInputSlot)
                {
                    if (fluidContainer.FluidId == fluidId && fluidContainer.Amount >= inputFluid.Amount)
                    {
                        found = true;
                        break;
                    }
                }
                
                if (!found) return false;
            }
            
            return true;
        }
    }
}