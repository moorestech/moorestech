using Client.Ui.Components.Slots;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots.Fluid
{
    /// <summary>
    /// FluidSlotRow の見本。タンクの 1 マスと矢印・機械の入出力・矢印なし
    /// FluidSlotRow samples: a tank cell with an arrow, machine input/output and no arrow
    /// </summary>
    public sealed class FluidSlotRowGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "FluidSlotRow";

        public VisualElement CreateSample()
        {
            var root = FluidGallerySampleSheet.CreateSampleRoot();
            var water = SlotGallerySampleSheet.Icon("10", "Water");

            // 4-block-tank: 50% のマス・空のマスと矢印
            // 4-block-tank: a 50% cell, an empty cell and the arrow
            var tank = new FluidSlotRow("gallery-fluid-row-tank");
            tank.SetFluids(new[] { FluidSlotContent.Filled(water, 500, 1000, FluidGallerySampleSheet.Water), FluidSlotContent.Empty });
            tank.SetProgress(0.5f);
            SlotGallerySampleSheet.AddPlainRow(root).Add(tank);

            // 矢印なしの 3 マス（ゴーストを含む）
            // Three cells without an arrow (including a ghost)
            var plain = new FluidSlotRow("gallery-fluid-row-plain");
            plain.SetFluids(new[] { FluidSlotContent.Filled(water, 120, 200, FluidGallerySampleSheet.Water), FluidSlotContent.Ghost(water, 2), FluidSlotContent.Empty });
            SlotGallerySampleSheet.AddPlainRow(root).Add(plain);
            return root;
        }
    }
}
