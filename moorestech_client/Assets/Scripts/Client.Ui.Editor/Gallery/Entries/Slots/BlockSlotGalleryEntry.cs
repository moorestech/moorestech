using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// BlockSlot の見本。絵のあるブロックと、絵が欠けて #id になるブロック
    /// BlockSlot samples: a block with a picture and one whose missing picture falls back to #id
    /// </summary>
    public sealed class BlockSlotGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "BlockSlot";

        public VisualElement CreateSample()
        {
            var root = SlotGallerySampleSheet.CreateSampleRoot();
            var row = SlotGallerySampleSheet.AddRow(root, "slot-gallery__plain");
            Add(row, "gallery-block-slot-icon").SetBlock(SlotGallerySampleSheet.Icon("12", "Assembler"), UiText.FromVerbatim("Assembler"));
            Add(row, "gallery-block-slot-missing").SetBlock(SlotGallerySampleSheet.MissingIcon("12", "Assembler"), UiText.FromVerbatim("Assembler"));
            return root;

            #region Internal

            BlockSlot Add(VisualElement parent, string testid)
            {
                var slot = new BlockSlot(testid);
                slot.AddToClassList("slot-gallery__cell");
                parent.Add(slot);
                return slot;
            }

            #endregion
        }
    }
}
