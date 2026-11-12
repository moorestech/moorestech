using Client.Tests.Ui.TestHost;
using System;
using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.Components.Gauges;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;
using Fixtures = Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection.MachineRecipeSelectionFixtures;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// レシピ選択の 1 行（名前・液体の枠・継承バッジ・押下）と、同じ並びの再描画で行を残すこと（D-37）
    /// One recipe-selection row (name, fluid slots, inheritance badge, press) and keeping rows on a same-order redraw (D-37)
    /// </summary>
    public class MachineRecipeSelectionRowTest : MachineScreenTestHost
    {
        private static readonly Guid RecipeA = MachineTestDetails.RecipeA;

        /// <web-test file="src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts">レシピ名を出力アイテム名で出し、行クリックで選択通知する</web-test>
        [Test]
        public void レシピ名は出力アイテム名で行押下で選択を通知する()
        {
            var view = MountRow(Fixtures.ItemRecipe(RecipeA, 7, SecondItem, FirstItem), true);
            var pressed = new List<Guid>();
            view.OnPressed.Subscribe(pressed.Add);

            Assert.IsTrue(view.ClassListContains(MachineRecipeSelectionRowView.SelectedClassName));
            Assert.AreEqual("Test Item 1", Find<Label>(view, $"machine-recipe-{RecipeA}-name").PlainText());
            simulate.Click(view.worldBound.center);
            CollectionAssert.AreEqual(new[] { RecipeA }, pressed);
        }

        /// <web-test file="src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts">代表が液体のときは液体名で行名を出す</web-test>
        [Test]
        public void 代表が液体なら液体名で行名を出す()
        {
            var recipe = Fixtures.Recipe(RecipeA, 7, new[] { new ItemAmount(FirstItem, 2) }, new RecipeFluidAmount[0], new ItemAmount[0], new[] { new RecipeFluidAmount(Water, 100) }, RecipeLevelInheritance.None);
            var view = MountRow(recipe, false);

            Assert.IsFalse(view.ClassListContains(MachineRecipeSelectionRowView.SelectedClassName));
            Assert.AreEqual("Test Water", Find<Label>(view, $"machine-recipe-{RecipeA}-name").PlainText());
        }

        /// <web-test file="src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts">入出力の液体はFluidAmountSlotへ量とtestIdを渡して描く</web-test>
        [Test]
        public void 入出力の液体は量の帯つきの液体の枠で描く()
        {
            var recipe = Fixtures.Recipe(RecipeA, 7, new[] { new ItemAmount(SecondItem, 2) }, new[] { new RecipeFluidAmount(Water, 10) }, new[] { new ItemAmount(FirstItem, 1) }, new[] { new RecipeFluidAmount(Water, 1000) }, RecipeLevelInheritance.None);
            var view = MountRow(recipe, false);

            var slots = view.Query<FluidAmountSlot>().ToList();
            Assert.AreEqual(2, slots.Count);
            foreach (var (side, amount) in new[] { ("input", "10"), ("output", "1,000") })
            {
                var frameName = $"machine-recipe-{RecipeA}-{side}-fluid-0";
                var slot = Find<VisualElement>(view, frameName).GetFirstAncestorOfType<FluidAmountSlot>();
                Assert.IsNotNull(slot, frameName);
                var band = Find<VisualElement>(slot, $"{frameName}-amount");
                Assert.AreEqual(amount, band.Q<Label>().PlainText(), frameName);
            }
        }

        /// <web-test file="src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts">上限つき継承レシピにはバッジを出す</web-test>
        [Test]
        public void 上限つき継承レシピにはバッジを出す()
        {
            var capped = RecipeLevelInheritance.InheritUpToUnlocked(0, new[] { SecondItem }, 0, new[] { FirstItem }, 2);
            var view = MountRow(Fixtures.Recipe(RecipeA, 7, new[] { new ItemAmount(SecondItem, 2) }, new RecipeFluidAmount[0], new[] { new ItemAmount(FirstItem, 1) }, new RecipeFluidAmount[0], capped), false);

            var expected = new UiText(LocalizationKeys.Ui.Recipe.LevelInheritanceCapped, new[] { new Client.Localization.LocalizationTextParam("level", "2") }).Resolve();
            Assert.AreEqual(expected, Find<Label>(view, $"machine-recipe-{RecipeA}-level-inheritance").PlainText());
        }

        /// <web-test file="src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts">継承しないレシピにはバッジを出さない</web-test>
        [Test]
        public void 継承しないレシピにはバッジを出さない()
        {
            var view = MountRow(Fixtures.ItemRecipe(RecipeA, 7, SecondItem, FirstItem), false);
            Assert.IsNull(view.Q($"machine-recipe-{RecipeA}-level-inheritance"));
        }

        [Test]
        public void 同じ並びの再描画では行の要素が同一のまま選択の印だけ動く()
        {
            // 加工中の機械には状態更新が毎 tick 届くので、並びが同じ間は行を作り直さない（D-37）
            // A processing machine gets state updates every tick, so rows are not rebuilt while the order is the same (D-37)
            var recipes = new[] { Fixtures.ItemRecipe(RecipeA, 5, SecondItem, FirstItem), Fixtures.ItemRecipe(MachineTestDetails.RecipeB, 7, FirstItem, SecondItem) };
            var list = Mount(new MachineRecipeSelectionList(Content, OsCursor));
            var leading = new VisualElement();
            list.Render(Fixtures.Machine(MachineRecipeSelection.Selected(RecipeA), false, recipes), leading);
            simulate.FrameUpdate();
            var rowA = Find<VisualElement>(list, $"machine-recipe-{RecipeA}");
            var rowB = Find<VisualElement>(list, $"machine-recipe-{MachineTestDetails.RecipeB}");
            var arrow = rowA.Q<ProgressArrowGlyph>($"machine-recipe-{RecipeA}-arrow");
            Assert.IsNotNull(arrow);
            Assert.IsTrue(rowA.ClassListContains(MachineRecipeSelectionRowView.SelectedClassName));

            // 同じカタログで選択だけを B へ
            // Only the selection moves to B, on the same catalog values
            list.Render(Fixtures.Machine(MachineRecipeSelection.Selected(MachineTestDetails.RecipeB), false, recipes), leading);
            Assert.AreSame(rowA, list.Q($"machine-recipe-{RecipeA}"));
            Assert.AreSame(rowB, list.Q($"machine-recipe-{MachineTestDetails.RecipeB}"));
            Assert.IsFalse(rowA.ClassListContains(MachineRecipeSelectionRowView.SelectedClassName));
            Assert.IsTrue(rowB.ClassListContains(MachineRecipeSelectionRowView.SelectedClassName));

            // 並びが変わると行を作り直す
            // A changed order rebuilds the rows
            list.Render(Fixtures.Machine(MachineRecipeSelection.Selected(RecipeA), false, recipes[1], recipes[0]), leading);
            Assert.AreNotSame(rowA, list.Q($"machine-recipe-{RecipeA}"));
        }

        private MachineRecipeSelectionRowView MountRow(MachineRecipeData recipe, bool selected)
        {
            // カタログ→行を通した 1 行を載せる
            // Mount one row built through catalog → rows
            var selection = selected ? MachineRecipeSelection.Selected(recipe.RecipeGuid) : MachineRecipeSelection.Unselected;
            var viewModel = Fixtures.Machine(selection, false, recipe);
            var view = Mount(new MachineRecipeSelectionRowView(Fixtures.Row(viewModel, recipe.RecipeGuid), Fixtures.RecipeOf(viewModel, recipe.RecipeGuid), Content, OsCursor));
            simulate.FrameUpdate();
            return view;
        }
    }
}
