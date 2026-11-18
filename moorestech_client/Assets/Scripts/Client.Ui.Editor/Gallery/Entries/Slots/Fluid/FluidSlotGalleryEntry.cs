using Client.Ui.Components.Slots;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots.Fluid
{
    /// <summary>
    /// FluidSlot の見本（参照値の 3 種と補助の 3 状態）
    /// FluidSlot samples: tank, pump and fluid platform (reference values) plus ghost, long fraction and a missing picture
    /// </summary>
    public sealed class FluidSlotGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "FluidSlot";

        public VisualElement CreateSample()
        {
            var root = FluidGallerySampleSheet.CreateSampleRoot();
            var water = SlotGallerySampleSheet.Icon("10", "Water");
            var row = SlotGallerySampleSheet.AddPlainRow(root);

            // 参照を撮った mock-host の値: 4-block-tank 500/1000（fixtures.ts の blockTank）・4-block-pump 120/200（extractionFixtures.ts）・4-block-trainFluidPlatform 250/1000（logisticsFixtures.ts）
            // Values from the mock-host that took the references: tank 500/1000, pump 120/200, train fluid platform 250/1000
            Add(row, FluidSlotContent.Filled(water, 500, 1000, FluidGallerySampleSheet.Water));
            Add(row, FluidSlotContent.Filled(water, 120, 200, FluidGallerySampleSheet.Water));
            Add(row, FluidSlotContent.Filled(water, 250, 1000, FluidGallerySampleSheet.Water));

            // 参照なしの見本
            // Samples not compared with references: empty, ghost, long fraction, missing picture
            Add(row, FluidSlotContent.Empty);
            Add(row, FluidSlotContent.Ghost(water, 2));
            Add(row, FluidSlotContent.Filled(water, 823.3333333333334, 1000, FluidGallerySampleSheet.Water));
            Add(row, FluidSlotContent.Filled(SlotGallerySampleSheet.MissingIcon("11", "Oil"), 700, 1000, FluidGallerySampleSheet.Oil));
            return root;

            #region Internal

            void Add(VisualElement parent, FluidSlotContent content)
            {
                var slot = new FluidSlot("fluid-slot");
                slot.AddToClassList("slot-gallery__cell");
                slot.SetFluid(content);
                parent.Add(slot);
            }

            #endregion
        }
    }
}
