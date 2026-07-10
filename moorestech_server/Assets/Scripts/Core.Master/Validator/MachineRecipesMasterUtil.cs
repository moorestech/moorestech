using System;
using System.Collections.Generic;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator
{
    public static class MachineRecipesMasterUtil
    {
        public static bool Validate(MachineRecipes machineRecipes, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += RecipeValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string RecipeValidation()
            {
                var logs = "";
                for (var i = 0; i < machineRecipes.Data.Length; i++)
                {
                    var recipe = machineRecipes.Data[i];
                    var recipeIndex = i;

                    if (recipe.MachineRecipeGuid == Guid.Empty)
                    {
                        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] uses reserved empty MachineRecipeGuid\n";
                    }

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

                    // 選択を曖昧にするGUID重複を拒否する
                    // Reject duplicate GUIDs because explicit selection would become ambiguous
                    for (var otherIndex = 0; otherIndex < recipeIndex; otherIndex++)
                    {
                        if (machineRecipes.Data[otherIndex].MachineRecipeGuid != recipe.MachineRecipeGuid) continue;
                        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] has duplicate MachineRecipeGuid:{recipe.MachineRecipeGuid} with Recipe[{otherIndex}]\n";
                        break;
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(
            MachineRecipes machineRecipes,
            out Dictionary<Guid, MachineRecipeMasterElement> machineRecipesByGuid)
        {
            machineRecipesByGuid = new Dictionary<Guid, MachineRecipeMasterElement>();
            
            // 検証済みGUIDの索引を構築する
            // Build the GUID lookup dictionary after validation succeeds
            foreach (var recipe in machineRecipes.Data)
            {
                machineRecipesByGuid.Add(recipe.MachineRecipeGuid, recipe);
            }
        }
    }
}
