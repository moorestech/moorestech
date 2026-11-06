using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// PlacementTargetFace の見本。ビルドメニューとホットバーの行を並べる
    /// PlacementTargetFace samples: build-menu and hotbar rows
    /// </summary>
    public sealed class PlacementTargetFaceGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "PlacementTargetFace";

        public VisualElement CreateSample()
        {
            var root = SlotGallerySampleSheet.CreateSampleRoot();
            foreach (var knobClass in new[] { "slot-gallery__build-menu", "slot-gallery__hotbar" })
            {
                var row = SlotGallerySampleSheet.AddRow(root, knobClass);
                Add(row).SetIconTarget(SlotGallerySampleSheet.Icon("3", "Chest"), UiText.FromVerbatim("Chest"));
                Add(row).SetNameTarget(UiText.FromVerbatim("Blueprint Copy"));
                Add(row).SetNameTarget(UiText.FromVerbatim("ElectricConveyorBeltJunction"));
            }
            return root;

            #region Internal

            PlacementTargetFace Add(VisualElement row)
            {
                var frame = new SlotFrame($"gallery-placement-{row.childCount}");
                frame.AddToClassList("slot-gallery__cell");
                frame.SetState(new SlotFrameState(false, true, false, false, false));
                var face = new PlacementTargetFace();
                frame.Add(face);
                row.Add(frame);
                return face;
            }

            #endregion
        }
    }
}
