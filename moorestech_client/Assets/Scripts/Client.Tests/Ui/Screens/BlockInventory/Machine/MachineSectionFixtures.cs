using System;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の画面テスト用の値
    /// Values for the machine-section screen tests; master must be loaded
    /// </summary>
    public static class MachineScreenViewModels
    {
        private static readonly SlotItem Empty = new(new ItemId(0), 0);

        public static OpenBlockInventoryPanel Panel()
        {
            return MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, Array.Empty<SlotItem>());
        }

        // 区画が描く状態の詳細。既定は最小構成
        // The detail the section draws; defaults to the minimal shape
        public static MachineDetail Detail(MachineProcessDisplayState state, double current, double request, bool gearDriven, bool outputSpaceShort)
        {
            return new MachineDetail(MachineRecipeSelection.Unselected, state, current, request, 0, outputSpaceShort, gearDriven, 0, Array.Empty<RecipeOutputItem>(), MachineTestDetails.Unbound(new MachineSlotLayout(1, 1, 0)));
        }

        public static MachineSectionViewModel Build(MachineDetail detail, SlotItem[] slots, MachineRecipeCatalog catalog, int moduleStackLimit, params BlockFluidSlotState[] fluids)
        {
            return MachineSectionViewModelBuilder.Build(MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, slots), detail, slots, fluids, catalog, moduleStackLimit);
        }

        public static MachineSectionViewModel WithState(MachineProcessDisplayState state, double current, double request, bool gearDriven, bool outputSpaceShort)
        {
            return Build(Detail(state, current, request, gearDriven, outputSpaceShort), new[] { Empty, Empty }, MachineRecipeCatalog.Empty, 1);
        }

        // 必要数の束縛があり枠 0 に held 個ある機械
        // A machine bound to `required`, slot 0 holding `held`
        public static MachineSectionViewModel WithInput(ItemId item, int required, int held)
        {
            var binding = new MachineBindingDetail(new MachineSlotLayout(1, 1, 0), new[] { new MachineItemSlotBinding(0, item, required, new[] { item }) }, Array.Empty<MachineTankBinding>());
            var detail = new MachineDetail(MachineRecipeSelection.Unselected, MachineProcessDisplayState.Processing, 1, 1, 0, false, false, 0, Array.Empty<RecipeOutputItem>(), binding);
            return Build(detail, new[] { new SlotItem(item, held), Empty }, MachineRecipeCatalog.Empty, 1);
        }

        // 束縛なしで出力の枠に count 個入った機械
        // An unbound machine whose output slot holds `count`
        public static MachineSectionViewModel WithFilledOutput(ItemId item, int count)
        {
            return Build(Detail(MachineProcessDisplayState.Idle, 1, 1, false, false), new[] { Empty, new SlotItem(item, count) }, MachineRecipeCatalog.Empty, 1);
        }

        // 加工中のレシピの出力と秒を持つ機械
        // A machine running a recipe with the given outputs and seconds
        public static MachineSectionViewModel Running(double recipeSeconds, params RecipeOutputItem[] outputs)
        {
            var detail = new MachineDetail(MachineRecipeSelection.Unselected, MachineProcessDisplayState.Processing, 1, 1, 0.5f, false, false, recipeSeconds, outputs, MachineTestDetails.Unbound(new MachineSlotLayout(1, 1, 0)));
            return Build(detail, new[] { Empty, Empty }, MachineRecipeCatalog.Empty, 1);
        }

        // 全枠に item を held 個ずつ入れた機械
        // A machine whose every slot holds `held` of the item
        public static MachineSectionViewModel Filled(int inputCount, int moduleCount, float progress, ItemId item, int held, int moduleStackLimit)
        {
            var slots = new SlotItem[inputCount + 1 + moduleCount];
            for (var i = 0; i < slots.Length; i++) slots[i] = new SlotItem(item, held);
            var detail = new MachineDetail(MachineRecipeSelection.Unselected, MachineProcessDisplayState.Processing, 1, 1, progress, false, false, 0, Array.Empty<RecipeOutputItem>(), MachineTestDetails.Unbound(new MachineSlotLayout(inputCount, 1, moduleCount)));
            return Build(detail, slots, MachineRecipeCatalog.Empty, moduleStackLimit);
        }

        // レシピ 1 件を解放し、selected なら選択済み
        // One unlocked recipe, selected if `selected`
        public static MachineSectionViewModel WithRecipe(MachineRecipeData recipe, bool selected, bool outputSpaceShort)
        {
            var selection = selected ? MachineRecipeSelection.Selected(recipe.RecipeGuid) : MachineRecipeSelection.Unselected;
            var detail = new MachineDetail(selection, MachineProcessDisplayState.Idle, 1, 1, 0, outputSpaceShort, false, 0, Array.Empty<RecipeOutputItem>(), MachineTestDetails.Unbound(new MachineSlotLayout(1, 1, 0)));
            return Build(detail, new[] { Empty, Empty }, MachineRecipeCatalog.Build(new[] { recipe }), 1);
        }

        public static MachineRecipeData Recipe(double time, ItemAmount[] outputItems, RecipeFluidAmount[] outputFluids)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(MachineTestDetails.MachineBlock.BlockGuid);
            return new MachineRecipeData(MachineTestDetails.RecipeA, blockId, time, Array.Empty<ItemAmount>(), outputItems, Array.Empty<RecipeFluidAmount>(), outputFluids, RecipeLevelInheritance.None);
        }
    }
}
