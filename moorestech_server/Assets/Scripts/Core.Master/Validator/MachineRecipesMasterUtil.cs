using System;
using System.Collections.Generic;
using System.Linq;
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

            #endregion
        }

        public static void Initialize(
            MachineRecipes machineRecipes,
            out Dictionary<string, MachineRecipeMasterElement> machineRecipesByRecipeKey)
        {
            machineRecipesByRecipeKey = new Dictionary<string, MachineRecipeMasterElement>();
            
            // レシピのDictionary構築（Validate成功後に実行）
            // Build recipe dictionary (executed after Validate succeeds)
            foreach (var recipe in machineRecipes.Data)
            {
                var inputItemIds = new List<ItemId>(recipe.InputItems.Length);
                foreach (var inputItem in recipe.InputItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                    inputItemIds.Add(itemId);
                }
                var fluidIds = new List<FluidId>(recipe.InputFluids.Length);
                foreach (var inputFluid in recipe.InputFluids)
                {
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                    fluidIds.Add(fluidId);
                }

                var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);

                var key = GetRecipeElementKey(blockId, inputItemIds, fluidIds);

                if (!machineRecipesByRecipeKey.TryAdd(key, recipe))
                {
                    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid);
                    var inputItemMaster = inputItemIds
                        .Select(i => MasterHolder.ItemMaster.GetItemMaster(i))
                        .ToList();

                    var recipe1Guid = machineRecipesByRecipeKey[key].MachineRecipeGuid;
                    var recipe2Guid = recipe.MachineRecipeGuid;
                    var recipe1Index = machineRecipes.Data.ToList().FindIndex(x => x.MachineRecipeGuid == recipe1Guid);
                    var recipe2Index = machineRecipes.Data.ToList().FindIndex(x => x.MachineRecipeGuid == recipe2Guid);

                    throw new Exception($"Duplicate recipe registered in machine recipe master. BlockID:{blockMaster.Name} ItemIDList: {string.Join(", ", inputItemMaster.Select(i => i.Name))} \n" +
                                        $"RecipeIndex1: {recipe1Index} RecipeIndex2: {recipe2Index} \n" +
                                        $"RecipeID1: {recipe1Guid} RecipeID2: {recipe2Guid}");
                }
            }
        }

        public static string GetRecipeElementKey(BlockId blockId, List<ItemId> itemIds, List<FluidId> fluidIds)
        {
            var items = new System.Text.StringBuilder();
            items.Append(blockId);

            itemIds.Sort((a, b) => a.AsPrimitive() - b.AsPrimitive());
            for (var i = 0; i < itemIds.Count; i++)
            {
                items.Append('_');
                items.Append(itemIds[i].AsPrimitive());
            }

            items.Append('|');
            for (var i = 0; i < fluidIds.Count; i++)
            {
                if (i > 0) items.Append('_');
                items.Append(fluidIds[i].AsPrimitive());
            }

            return items.ToString();
        }
    }
}
