using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Machine.RecipeSelection;
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

            // 束縛規則の判定は束縛ユーティリティ1箇所へ集約し、ここは数量の充足だけを見る
            // The binding rule itself lives solely in the binding util; this method only checks the quantities
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                if (inputSlot.Count <= i) return false;
                if (!MachineRecipeSlotBindingUtil.IsInputBoundTo(recipe, i, inputSlot[i].Id)) return false;
                if (inputSlot[i].Count < recipe.InputItems[i].Count) return false;
            }

            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                if (fluidInputSlot.Count <= i) return false;
                if (!MachineRecipeSlotBindingUtil.IsInputFluidBoundTo(recipe, i, fluidInputSlot[i].FluidId)) return false;
                if (fluidInputSlot[i].Amount < recipe.InputFluids[i].Amount) return false;
            }
            return true;
        }
    }
}
