using System;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 選択モードの出し分け（Web MachineSection の showSelection・C14）
    /// Selection-mode visibility (Web MachineSection showSelection, C14)
    /// </summary>
    public class MachineSelectionModeStateTest
    {
        private static readonly Guid RecipeA = MachineTestDetails.RecipeA;
        private static readonly Guid RecipeB = MachineTestDetails.RecipeB;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 未選択は選択モードでレシピ0件はインベントリモード()
        {
            var state = new MachineSelectionModeState();
            Assert.IsTrue(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected, RecipeA, RecipeB)));
            Assert.IsFalse(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected)));
        }

        [Test]
        public void 開いた後は要求がサーバーの選択に一致するまで閉じない()
        {
            var state = new MachineSelectionModeState();
            var selectedA = MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(RecipeA), RecipeA, RecipeB);
            Assert.IsFalse(state.ShowsSelection(selectedA));
            state.OpenSelection();
            Assert.IsTrue(state.ShowsSelection(selectedA));
            state.RememberRequest(RecipeB);
            Assert.IsTrue(state.ShowsSelection(selectedA));
            Assert.IsFalse(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(RecipeB), RecipeA, RecipeB)));
        }

        [Test]
        public void 同じレシピを選び直しても閉じる()
        {
            var state = new MachineSelectionModeState();
            var selectedA = MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(RecipeA), RecipeA, RecipeB);
            state.OpenSelection();
            state.RememberRequest(RecipeA);
            Assert.IsFalse(state.ShowsSelection(selectedA));
        }

        [Test]
        public void 選択中のレシピが行に無ければ選択モード()
        {
            var state = new MachineSelectionModeState();
            Assert.IsTrue(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(Guid.NewGuid()), RecipeA)));
        }
    }
}
