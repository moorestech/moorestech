using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator
{
    public static class MachineRecipesMasterUtil
    {
        public static bool Validate(MachineRecipes machineRecipes, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += RecipeValidation();
            errorLogs += SlotCapacityValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string RecipeValidation()
            {
                var logs = "";
                for (var i = 0; i < machineRecipes.Data.Length; i++)
                {
                    var recipe = machineRecipes.Data[i];
                    var recipeIndex = i;

                    // blockGuidのチェック
                    // Check blockGuid
                    var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(recipe.BlockGuid);
                    if (blockId == null)
                    {
                        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] has invalid BlockGuid:{recipe.BlockGuid}\n";
                    }

                    // inputItemsのチェック
                    // Check inputItems
                    foreach (var inputItem in recipe.InputItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(inputItem.ItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] has invalid InputItem.ItemGuid:{inputItem.ItemGuid}\n";
                        }
                    }

                    // outputItemsのチェック
                    // Check outputItems
                    foreach (var outputItem in recipe.OutputItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(outputItem.ItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] has invalid OutputItem.ItemGuid:{outputItem.ItemGuid}\n";
                        }
                    }

                    // inputFluidsのチェック
                    // Check inputFluids
                    foreach (var inputFluid in recipe.InputFluids)
                    {
                        var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(inputFluid.FluidGuid);
                        if (fluidId == null)
                        {
                            logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] has invalid InputFluid.FluidGuid:{inputFluid.FluidGuid}\n";
                        }
                    }

                    // outputFluidsのチェック
                    // Check outputFluids
                    foreach (var outputFluid in recipe.OutputFluids)
                    {
                        var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(outputFluid.FluidGuid);
                        if (fluidId == null)
                        {
                            logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] has invalid OutputFluid.FluidGuid:{outputFluid.FluidGuid}\n";
                        }
                    }

                    // 空のレシピのチェック（入力も出力もないレシピは無効）
                    // Check for empty recipe (recipe with no inputs and no outputs is invalid)
                    var hasNoInput = recipe.InputItems.Length == 0 && recipe.InputFluids.Length == 0;
                    var hasNoOutput = recipe.OutputItems.Length == 0 && recipe.OutputFluids.Length == 0;
                    if (hasNoInput && hasNoOutput)
                    {
                        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] GUID:{recipe.MachineRecipeGuid} is empty (no inputs and no outputs)\n";
                    }
                    else if (hasNoInput)
                    {
                        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] GUID:{recipe.MachineRecipeGuid} has no inputs (inputItems and inputFluids are both empty)\n";
                    }
                    else if (hasNoOutput)
                    {
                        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] GUID:{recipe.MachineRecipeGuid} has no outputs (outputItems and outputFluids are both empty)\n";
                    }
                }

                return logs;
            }

            // レシピ品目数がブロックの物理スロット数を超えないこと。超えると束縛先が無く機械が恒久停止する
            // Recipe item counts must not exceed the block's physical slots; an overflow leaves no binding target and stalls the machine forever
            string SlotCapacityValidation()
            {
                var logs = "";
                foreach (var recipe in machineRecipes.Data)
                {
                    var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(recipe.BlockGuid);
                    if (blockId == null) continue;
                    if (MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid).BlockParam is not IMachineParam machineParam) continue;

                    logs += CheckCapacity("inputItems", recipe.InputItems.Length, machineParam.InputSlotCount);
                    logs += CheckCapacity("outputItems", recipe.OutputItems.Length, machineParam.OutputSlotCount);
                    logs += CheckCapacity("inputFluids", recipe.InputFluids.Length, machineParam.InputTankCount);
                    logs += CheckCapacity("outputFluids", recipe.OutputFluids.Length, machineParam.OutputTankCount);

                    string CheckCapacity(string fieldName, int count, int slotCount)
                    {
                        if (count <= slotCount) return "";
                        return $"[MachineRecipesMaster] Recipe GUID:{recipe.MachineRecipeGuid} has {count} {fieldName} but the block only has {slotCount} slots\n";
                    }
                }
                return logs;
            }

            #endregion
        }
    }
}
