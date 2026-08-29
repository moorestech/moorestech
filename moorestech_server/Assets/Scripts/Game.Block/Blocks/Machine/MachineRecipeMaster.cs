using System.Collections.Generic;
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

            // 素材iはスロットiに束縛されているので番号で照合する
            // Input i is bound to slot i, so match by index
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                if (inputSlot.Count <= i) return false;
                var required = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[i].ItemGuid);
                if (inputSlot[i].Id != required || inputSlot[i].Count < recipe.InputItems[i].Count) return false;
            }

            // 液体iはタンクiに束縛されているので番号で照合する
            // Fluid i is bound to tank i, so match by index
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                if (fluidInputSlot.Count <= i) return false;
                var required = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[i].FluidGuid);
                if (fluidInputSlot[i].FluidId != required || fluidInputSlot[i].Amount < recipe.InputFluids[i].Amount) return false;
            }
            return true;
        }
    }
}
