using System;
using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// レシピ選択の画面テスト用の値（マスタ読み込み済みが前提。レシピ・機械の値・選択行を組む）
    /// Values for the recipe-selection screen tests (master must be loaded; builds recipes, machine values and selection rows)
    /// </summary>
    public static class MachineRecipeSelectionFixtures
    {
        public static readonly Guid RecipeC = new("00000000-0000-0000-0000-00000000c00c");

        // 開いた機械のブロックに属するレシピ
        // A recipe that belongs to the open machine's block
        public static MachineRecipeData Recipe(Guid recipeGuid, double time, ItemAmount[] inputItems, RecipeFluidAmount[] inputFluids, ItemAmount[] outputItems, RecipeFluidAmount[] outputFluids, RecipeLevelInheritance inheritance)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(MachineTestDetails.MachineBlock.BlockGuid);
            return new MachineRecipeData(recipeGuid, blockId, time, inputItems, outputItems, inputFluids, outputFluids, inheritance);
        }

        // 素材 1 品から 1 品を作るだけのレシピ
        // A recipe that only turns one item into another
        public static MachineRecipeData ItemRecipe(Guid recipeGuid, double time, ItemId input, ItemId output)
        {
            return Recipe(recipeGuid, time, new[] { new ItemAmount(input, 1) }, Array.Empty<RecipeFluidAmount>(), new[] { new ItemAmount(output, 1) }, Array.Empty<RecipeFluidAmount>(), RecipeLevelInheritance.None);
        }

        // 束縛なし・入力 1・出力 1 の機械に、渡したレシピを解放済みにする
        // An unbound 1-input, 1-output machine with the given recipes unlocked
        public static MachineSectionViewModel Machine(MachineRecipeSelection selection, bool outputSpaceShort, params MachineRecipeData[] recipes)
        {
            var detail = new MachineDetail(selection, MachineProcessDisplayState.Idle, 1, 1, 0, outputSpaceShort, false, 0, Array.Empty<RecipeOutputItem>(), MachineTestDetails.Unbound(new MachineSlotLayout(1, 1, 0)));
            var slots = new[] { new SlotItem(new ItemId(0), 0), new SlotItem(new ItemId(0), 0) };
            return MachineScreenViewModels.Build(detail, slots, MachineRecipeCatalog.Build(recipes), 1);
        }

        // 選んだレシピの素材・結果をそのまま束縛にした機械（入力 2・出力 1・入力タンク 1・出力タンク 1。Web の machine の形）
        // A machine bound to the selected recipe's materials and results (2 inputs, 1 output, 1+1 tanks; the Web machine fixture shape)
        public static MachineSectionViewModel BoundMachine(Guid selectedRecipeGuid, IReadOnlyList<MachineRecipeData> recipes, SlotItem[] slots)
        {
            var selected = recipes.First(recipe => recipe.RecipeGuid == selectedRecipeGuid);
            var itemBindings = new List<MachineItemSlotBinding>();
            for (var i = 0; i < selected.InputItems.Count; i++) itemBindings.Add(new MachineItemSlotBinding(i, selected.InputItems[i].ItemId, selected.InputItems[i].Count, new[] { selected.InputItems[i].ItemId }));
            for (var i = 0; i < selected.OutputItems.Count; i++) itemBindings.Add(new MachineItemSlotBinding(2 + i, selected.OutputItems[i].ItemId, selected.OutputItems[i].Count, new[] { selected.OutputItems[i].ItemId }));
            var tankBindings = new List<MachineTankBinding>();
            for (var i = 0; i < selected.InputFluids.Count; i++) tankBindings.Add(new MachineTankBinding(i, selected.InputFluids[i].FluidId, selected.InputFluids[i].Amount));
            for (var i = 0; i < selected.OutputFluids.Count; i++) tankBindings.Add(new MachineTankBinding(1 + i, selected.OutputFluids[i].FluidId, selected.OutputFluids[i].Amount));

            var binding = new MachineBindingDetail(new MachineSlotLayout(2, 1, 0), itemBindings, tankBindings);
            var detail = new MachineDetail(MachineRecipeSelection.Selected(selectedRecipeGuid), MachineProcessDisplayState.Idle, 1, 1, 0, false, false, selected.Time, Array.Empty<RecipeOutputItem>(), binding);
            var fluids = new[] { BlockFluidSlotState.Empty(100), BlockFluidSlotState.Empty(2000) };
            return MachineScreenViewModels.Build(detail, slots, MachineRecipeCatalog.Build(recipes), 1, fluids);
        }

        // 値の中の行（GUID で引く）
        // The row of the value, looked up by GUID
        public static MachineRecipeSelectionRow Row(MachineSectionViewModel viewModel, Guid recipeGuid)
        {
            return viewModel.Rows.First(row => row.Recipe.RecipeGuid == recipeGuid);
        }

        public static MachineRecipeData RecipeOf(MachineSectionViewModel viewModel, Guid recipeGuid)
        {
            viewModel.TryGetRecipe(recipeGuid, out var recipe);
            return recipe;
        }

        // GUID の並びが 1 から n まで続くアイテムレシピ（溢れの検査用）
        // Item recipes with GUIDs numbered 1..n (for the overflow check)
        public static MachineRecipeData[] ManyItemRecipes(int count, ItemId input, ItemId output)
        {
            return Enumerable.Range(1, count).Select(i => ItemRecipe(new Guid($"00000000-0000-0000-0000-{i:D12}"), 5, input, output)).ToArray();
        }
    }
}
