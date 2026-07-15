using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master
{
    public static class MachineRecipeMasterUtil
    {
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
            // Check that enough item slots satisfy the required counts
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
            // Check that fluid inputs are satisfied
            foreach (var inputFluid in recipe.InputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                var found = false;

                // 任意のスロットに必要な液体があるかチェック
                // Check any slot for the required fluid amount
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
