using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Icons;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots.Fluid
{
    /// <summary>
    /// FluidAmountSlot の見本。既定の行と機械のレシピ選択の行
    /// FluidAmountSlot samples: a default row and the machine recipe selection row
    /// </summary>
    public sealed class FluidAmountSlotGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "FluidAmountSlot";

        public VisualElement CreateSample()
        {
            var root = FluidGallerySampleSheet.CreateSampleRoot();
            var water = SlotGallerySampleSheet.Icon("10", "Water");
            var oil = SlotGallerySampleSheet.Icon("11", "Oil");
            var sampleIndex = 0;

            // 既定のつまみ: 量・0・4 桁・量なし・欠けた絵
            // Default knobs: amount, zero, four digits, hidden and a missing picture
            var plain = SlotGallerySampleSheet.AddPlainRow(root);
            Add(plain, oil, SlotAmountBadge.Show(10));
            Add(plain, oil, SlotAmountBadge.Show(0));
            Add(plain, oil, SlotAmountBadge.Show(1000));
            Add(plain, oil, SlotAmountBadge.Hidden);
            Add(plain, SlotGallerySampleSheet.MissingIcon("11", "Oil"), SlotAmountBadge.Show(10));

            // 4-block-machine-recipe-selection の Oil 行（recipeFixtures.ts の液体だけのレシピ: 入力 Water 10 → 出力 Oil 1,000）
            // The Oil row of 4-block-machine-recipe-selection (the fluid-only recipe in recipeFixtures.ts: Water 10 in, Oil 1,000 out)
            var recipe = SlotGallerySampleSheet.AddRow(root, "fluid-gallery__recipe-row");
            Add(recipe, water, SlotAmountBadge.Show(10));
            Add(recipe, oil, SlotAmountBadge.Show(1000));
            return root;

            #region Internal

            void Add(VisualElement parent, UiIcon icon, SlotAmountBadge badge)
            {
                var slot = new FluidAmountSlot($"gallery-fluid-amount-{sampleIndex++}");
                slot.AddToClassList("slot-gallery__cell");
                slot.SetFluid(icon, badge);
                parent.Add(slot);
            }

            #endregion
        }
    }
}
