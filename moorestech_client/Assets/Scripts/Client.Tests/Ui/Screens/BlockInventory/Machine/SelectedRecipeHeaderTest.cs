using Client.Ui.Components.Slots;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 選択中レシピのヘッダの代表の枠と名前（区画を偽の Model で組む）
    /// The selected-recipe header's representative slot and name (section on a fake model)
    /// </summary>
    public class SelectedRecipeHeaderTest : MachineScreenTestHost
    {
        private VisualElement MountSelected(MachineRecipeData recipe)
        {
            return MountSection(MachineScreenViewModels.WithRecipe(recipe, true, false), false).Root;
        }

        /// <web-test file="src/features/blockInventory/details/machine/SelectedRecipeHeader.test.ts">代表が液体のときはFluidAmountSlotをバッジ非表示で描く</web-test>
        [Test]
        public void 代表が液体なら量の帯の無い液体の枠を描く()
        {
            var root = MountSelected(MachineScreenViewModels.Recipe(7, new ItemAmount[0], new[] { new RecipeFluidAmount(Water, 100) }));
            var header = Find<VisualElement>(root, "machine-selected-recipe");

            Assert.IsNotNull(header.Q<FluidAmountSlot>());
            Assert.IsNotNull(header.Q("machine-selected-recipe-fluid"));
            Assert.IsNull(header.Q("machine-selected-recipe-fluid-amount"));
            Assert.IsNull(header.Q<ItemSlot>());
        }

        /// <web-test file="src/features/blockInventory/details/machine/SelectedRecipeHeader.test.ts">代表がアイテムのときはItemSlotを描く</web-test>
        [Test]
        public void 代表がアイテムならItemSlotを描く()
        {
            var root = MountSelected(MachineScreenViewModels.Recipe(7, new[] { new ItemAmount(FirstItem, 1) }, new RecipeFluidAmount[0]));
            var header = Find<VisualElement>(root, "machine-selected-recipe");

            Assert.IsNotNull(header.Q<ItemSlot>());
            Assert.IsNotNull(header.Q("machine-selected-recipe-item"));
            Assert.IsNull(header.Q<FluidAmountSlot>());
            Assert.AreEqual("7 seconds", header.Q<Label>("machine-selected-recipe-time").text);
        }

        [Test]
        public void 出力2品でもヘッダは代表の名前だけ()
        {
            // 「他 N 件」は選択リストの行だけ（Web SelectedRecipeHeader の useRecipeDisplayName）
            // "and N more" belongs to the selection rows only (Web SelectedRecipeHeader useRecipeDisplayName)
            var root = MountSelected(MachineScreenViewModels.Recipe(7, new[] { new ItemAmount(FirstItem, 1), new ItemAmount(SecondItem, 1) }, new RecipeFluidAmount[0]));

            Assert.AreEqual("Test Item 1", root.Q<Label>("machine-selected-recipe-name").text);
        }
    }
}
