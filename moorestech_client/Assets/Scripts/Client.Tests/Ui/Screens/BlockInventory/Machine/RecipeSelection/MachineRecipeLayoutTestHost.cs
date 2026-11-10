using System;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using UniRx;
using UnityEngine.UIElements;
using Fixtures = Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection.MachineRecipeSelectionFixtures;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// レシピ選択の配置の写しの土台
    /// Base of the recipe-selection layout ports
    /// </summary>
    public abstract class MachineRecipeLayoutTestHost : MachineScreenTestHost
    {
        protected const string ListScroll = MachineRecipeSelectionList.ListTestid + "-scroll";
        protected static readonly Guid RecipeA = MachineTestDetails.RecipeA;
        protected static readonly Guid RecipeB = MachineTestDetails.RecipeB;
        protected static readonly Guid RecipeC = Fixtures.RecipeC;

        protected BlockInventoryTestShell Shell { get; private set; }

        protected MachineRecipeData[] Recipes()
        {
            // Web の machine の 3 件
            // The Web machine's three: A (5 s), B (10 s, fluid input), C (fluid-only output)
            var a = Fixtures.ItemRecipe(RecipeA, 5, SecondItem, FirstItem);
            var b = Fixtures.Recipe(RecipeB, 10, new[] { new ItemAmount(SecondItem, 1) }, new[] { new RecipeFluidAmount(Water, 10) }, new[] { new ItemAmount(FirstItem, 1) }, new RecipeFluidAmount[0], RecipeLevelInheritance.None);
            var c = Fixtures.Recipe(RecipeC, 3, new ItemAmount[0], new[] { new RecipeFluidAmount(Water, 10) }, new ItemAmount[0], new[] { new RecipeFluidAmount(Water, 1000) }, RecipeLevelInheritance.None);
            return new[] { a, b, c };
        }

        protected void OpenMachine(Guid selectedRecipe, GameContentPresentation content)
        {
            Open(MachinePanel(), Bound(selectedRecipe), content);
        }

        // 入力 2・出力 1 の機械の枠（入力 0 に素材が 1 個）
        // Slots of the 2-input, 1-output machine (input 0 holds one material)
        private static SlotItem[] Slots()
        {
            return new[] { new SlotItem(SecondItem, 1), new SlotItem(new ItemId(0), 0), new SlotItem(new ItemId(0), 0) };
        }

        protected static OpenBlockInventoryPanel MachinePanel()
        {
            return MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, Slots());
        }

        protected MachineSectionViewModel Bound(Guid selectedRecipe)
        {
            return Fixtures.BoundMachine(selectedRecipe, Recipes(), Slots());
        }

        // サーバーの選択が追いついた状態を流す（送信は記録係なので選択は変わらない）
        // Push the state where the server selection caught up (the recorder never changes it)
        protected void SetBound(Guid selectedRecipe)
        {
            SetMachine(Bound(selectedRecipe));
        }

        protected void SetMachine(MachineSectionViewModel machine)
        {
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(machine));
            Settle();
        }

        protected void Open(BlockInventoryPanelState panel, MachineSectionViewModel machine, GameContentPresentation content)
        {
            // 本番と同じく機械・文脈の工場と寄与を差す
            // Plug machine and context factories and the large rule, as the real GameUi
            var rule = new RecipeMachineLargePanelRule(MachineModel);
            rule.Initialize();
            var factory = new MachineSectionFactory(MachineModel, ContextModel, Commands, Toaster, content, RecipeSelect, OsCursor);

            // 外殻は閉じたまま先に載り、値は既に届く
            // The shell mounts closed first and the machine value is already there on open
            var panelState = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            Shell = BlockInventoryTestShell.Create(this, panelState, rule, factory, new ContextSectionFactory(content, ContextModel, MachineModel));
            Settle();
            if (machine != null) MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(machine));
            panelState.Value = panel;
            Settle();
        }

        protected void Click(string testid)
        {
            simulate.Click(Q(testid).worldBound.center);
            Settle();
        }

        protected void Settle()
        {
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        protected VisualElement Q(string testid) => Find<VisualElement>(Shell.View, testid);
        protected T Q<T>(string testid) where T : VisualElement => Find<T>(Shell.View, testid);
        protected int Ghosts(string band) => Q(band).Query<SlotFrame>(className: "slot-frame--ghost").ToList().Count + Q(band).Query<FluidSlot>(className: "fluid-slot--ghost").ToList().Count;
    }
}
