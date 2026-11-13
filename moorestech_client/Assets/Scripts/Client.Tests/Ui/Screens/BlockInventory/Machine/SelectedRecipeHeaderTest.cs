using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 選択中レシピのヘッダの代表の枠と名前
    /// The selected-recipe header's representative slot and name
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
            Assert.AreEqual("7 seconds", header.Q<Label>("machine-selected-recipe-time").PlainText());
        }

        [Test]
        public void 出力2品でもヘッダは代表の名前だけ()
        {
            // 「他 N 件」は選択リストの行だけ
            // "and N more" belongs to the selection rows only
            var root = MountSelected(MachineScreenViewModels.Recipe(7, new[] { new ItemAmount(FirstItem, 1), new ItemAmount(SecondItem, 1) }, new RecipeFluidAmount[0]));

            Assert.AreEqual("Test Item 1", root.Q<Label>("machine-selected-recipe-name").PlainText());
        }

        [Test]
        public void ヘッダに乗ると指を要求し離れると外す()
        {
            // USS の cursor はランタイムで効かないので、乗っている間だけ OS カーソルへ指を要求する（web: cursor: pointer）
            // USS cursor does nothing at runtime, so the pointer is requested on the OS cursor only while hovered (web: cursor: pointer)
            var root = MountSelected(MachineScreenViewModels.Recipe(7, new[] { new ItemAmount(FirstItem, 1) }, new RecipeFluidAmount[0]));
            var header = Find<VisualElement>(root, "machine-selected-recipe");
            var outside = new Vector2(header.worldBound.xMax + 50f, header.worldBound.yMax + 50f);
            simulate.MouseMove(outside);
            simulate.MouseMove(header.worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, AppliedCursor.Current, "pointer while over the header");

            simulate.MouseMove(outside);
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, AppliedCursor.Current, "withdrawn after leaving");
        }
    }
}
