using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// サーバーの選択レシピの文字列を未選択／選択中の union に解く境界（L0-s4b D-27）
    /// The boundary that parses the server's selected-recipe string into the unselected/selected union (L0-s4b D-27)
    /// </summary>
    public class MachineRecipeSelectionTest
    {
        private static readonly Guid BlockGuid = new("00000000-0000-0000-0000-0000000000b1");
        private static readonly Guid RecipeA = new("00000000-0000-0000-0000-00000000000a");

        [Test]
        public void 空GUIDは未選択に解く()
        {
            Assert.IsTrue(MachineRecipeSelection.TryParse(Guid.Empty.ToString(), out var selection));
            Assert.IsInstanceOf<UnselectedMachineRecipe>(selection);
            Assert.IsFalse(selection.Matches(Guid.Empty));
        }

        [Test]
        public void GUIDは選択中に解き同じGUIDにだけ一致する()
        {
            Assert.IsTrue(MachineRecipeSelection.TryParse(RecipeA.ToString(), out var selection));
            Assert.AreEqual(RecipeA, ((SelectedMachineRecipe)selection).RecipeGuid);
            Assert.IsTrue(selection.Matches(RecipeA));
            Assert.IsFalse(selection.Matches(Guid.NewGuid()));
        }

        [Test]
        public void 読めない文字列は偽を返す()
        {
            Assert.IsFalse(MachineRecipeSelection.TryParse("not-a-guid", out _));
            Assert.IsFalse(MachineRecipeSelection.TryParse(null, out _));
        }

        [Test]
        public void 同じ選択どうしだけが同じと判定される()
        {
            // 行の覚え（D-39）は選択が同じ間だけ前の行を使い回す
            // The row cache (D-39) reuses the previous rows only while the selection is the same
            Assert.IsTrue(MachineRecipeSelection.Unselected.IsSame(MachineRecipeSelection.Unselected));
            Assert.IsTrue(MachineRecipeSelection.Selected(RecipeA).IsSame(MachineRecipeSelection.Selected(RecipeA)));
            Assert.IsFalse(MachineRecipeSelection.Selected(RecipeA).IsSame(MachineRecipeSelection.Unselected));
            Assert.IsFalse(MachineRecipeSelection.Selected(RecipeA).IsSame(MachineRecipeSelection.Selected(Guid.NewGuid())));
        }

        [Test]
        public void 選択中だけが行に選択の印を付ける()
        {
            var recipes = new List<MachineRecipeSummary> { new(RecipeA, BlockGuid, new[] { new RecipeOutputItem(new ItemId(1), 1) }, Array.Empty<RecipeOutputFluid>()) };
            Assert.IsFalse(MachineRecipeSelection.Unselected.BuildRows(recipes, BlockGuid)[0].Selected);
            Assert.IsTrue(MachineRecipeSelection.Selected(RecipeA).BuildRows(recipes, BlockGuid)[0].Selected);
        }
    }
}
