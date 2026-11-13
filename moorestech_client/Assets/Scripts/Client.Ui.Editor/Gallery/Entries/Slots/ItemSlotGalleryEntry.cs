using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// ItemSlot の見本。持ち物・機械・カタログの行を並べる
    /// ItemSlot samples: inventory, machine and catalog rows
    /// </summary>
    public sealed class ItemSlotGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "ItemSlot";

        public VisualElement CreateSample()
        {
            var root = SlotGallerySampleSheet.CreateSampleRoot();
            var icon = SlotGallerySampleSheet.Icon("1", "Iron Plate");
            // testid は行をまたいで一意にするため見本全体の通し番号で付ける
            // testids use a running number across the whole sample so they stay unique across rows
            var sampleIndex = 0;

            // 持ち物（4-player-inventory）
            // Inventory (4-player-inventory)
            var inventory = SlotGallerySampleSheet.AddRow(root, "slot-gallery__inventory");
            Add(inventory, new ItemSlotContent(icon, 1, ItemSlotMarks.None, null, null, null));
            Add(inventory, new ItemSlotContent(icon, 12, ItemSlotMarks.None, null, null, null));
            Add(inventory, new ItemSlotContent(icon, 1500, ItemSlotMarks.None, null, null, null));
            Add(inventory, new ItemSlotContent(icon, null, ItemSlotMarks.None, null, null, null));
            Add(inventory, new ItemSlotContent(null, null, ItemSlotMarks.None, null, null, null));
            Add(inventory, new ItemSlotContent(icon, 3, new ItemSlotMarks(true, false, false), null, null, null));

            // 機械の素材（4-block-machine）
            // Machine materials (4-block-machine)
            var machine = SlotGallerySampleSheet.AddRow(root, "slot-gallery__machine-input");
            Add(machine, new ItemSlotContent(icon, 5, ItemSlotMarks.None, null, null, null));
            Add(machine, new ItemSlotContent(icon, 1, ItemSlotMarks.None, new ItemSlotShortage(1, 5, true, UiText.FromVerbatim("Iron Plate 1/5")), null, null));
            Add(machine, new ItemSlotContent(icon, 5, ItemSlotMarks.None, new ItemSlotShortage(5, 5, false, null), null, null));
            Add(machine, new ItemSlotContent(icon, 5, new ItemSlotMarks(false, false, true), null, null, null));
            Add(machine, new ItemSlotContent(null, null, ItemSlotMarks.None, null, new ItemSlotGhost(icon, 2), null));

            // カタログ（4-item-list-large）: 未所持の灰面・所持の白面
            // Catalog (4-item-list-large): unowned gray and owned white
            var catalog = SlotGallerySampleSheet.AddRow(root, "slot-gallery__item-list");
            Add(catalog, new ItemSlotContent(icon, 0, new ItemSlotMarks(false, true, false), null, null, null));
            Add(catalog, new ItemSlotContent(icon, 24, new ItemSlotMarks(false, true, false), null, null, null));
            return root;

            #region Internal

            void Add(VisualElement row, ItemSlotContent content)
            {
                var slot = new ItemSlot($"gallery-item-slot-{sampleIndex++}");
                slot.AddToClassList("slot-gallery__cell");
                slot.SetContent(content);
                row.Add(slot);
            }

            #endregion
        }
    }
}
