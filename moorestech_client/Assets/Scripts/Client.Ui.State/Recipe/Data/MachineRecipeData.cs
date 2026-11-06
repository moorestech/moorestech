using System;
using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.Recipe.Data
{
    /// <summary>
    /// 解放済み機械レシピ 1 件の値（Web bridge の MachineRecipe の写し。blockGuid は BlockId と同じものなので持たない）
    /// One unlocked machine recipe (port of the Web bridge MachineRecipe; blockGuid is the same as BlockId, so it is not kept)
    /// </summary>
    public sealed class MachineRecipeData
    {
        public Guid RecipeGuid { get; }
        public BlockId BlockId { get; }
        public double Time { get; }
        public IReadOnlyList<ItemAmount> InputItems { get; }
        public IReadOnlyList<ItemAmount> OutputItems { get; }
        public IReadOnlyList<RecipeFluidAmount> InputFluids { get; }
        public IReadOnlyList<RecipeFluidAmount> OutputFluids { get; }
        public RecipeLevelInheritance LevelInheritance { get; }

        public MachineRecipeData(Guid recipeGuid, BlockId blockId, double time, IReadOnlyList<ItemAmount> inputItems, IReadOnlyList<ItemAmount> outputItems, IReadOnlyList<RecipeFluidAmount> inputFluids, IReadOnlyList<RecipeFluidAmount> outputFluids, RecipeLevelInheritance levelInheritance)
        {
            RecipeGuid = recipeGuid;
            BlockId = blockId;
            Time = time;
            InputItems = inputItems;
            OutputItems = outputItems;
            InputFluids = inputFluids;
            OutputFluids = outputFluids;
            LevelInheritance = levelInheritance;
        }
    }
}
