using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Recipe.Data;
using Core.Master;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// 機械レシピを選択行が読む要約へ写す（Web の MachineRecipe と同じ形を L0-s4b の BuildRows へ渡す）
    /// Maps machine recipes onto the summaries selection rows read (feeds L0-s4b BuildRows the Web MachineRecipe shape)
    /// </summary>
    public static class MachineRecipeSummaryConversion
    {
        public static IReadOnlyList<MachineRecipeSummary> ToSummaries(IReadOnlyList<MachineRecipeData> recipes)
        {
            var summaries = new List<MachineRecipeSummary>(recipes.Count);
            foreach (var recipe in recipes)
            {
                var outputItems = new List<RecipeOutputItem>();
                foreach (var item in recipe.OutputItems) outputItems.Add(new RecipeOutputItem(item.ItemId, item.Count));
                var outputFluids = new List<RecipeOutputFluid>();
                foreach (var fluid in recipe.OutputFluids) outputFluids.Add(new RecipeOutputFluid(fluid.FluidId, fluid.Amount));
                var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockId).BlockGuid;
                summaries.Add(new MachineRecipeSummary(recipe.RecipeGuid, blockGuid, outputItems, outputFluids));
            }
            return summaries;
        }
    }
}
