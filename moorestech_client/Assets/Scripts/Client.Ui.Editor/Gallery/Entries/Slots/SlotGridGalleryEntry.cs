using Client.Ui.Components.Slots;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// SlotGrid の見本。持ち物の 10 列 45 マス（参照と同じ個数の並び。5 行目は 5 マス）と、レシピ一覧の 6 列のカタログ
    /// SlotGrid samples: the inventory 10 columns, 45 cells (same count layout as the reference; row 5 has 5 cells) and the 6-column recipe catalog
    /// </summary>
    public sealed class SlotGridGalleryEntry : IUiGalleryEntry
    {
        // 4-player-inventory の持ち物の個数（0 は空きマス）。書く前に参照の画像と 1 マスずつ突き合わせる
        // Counts in the 4-player-inventory inventory (0 is an empty cell); check each cell against the reference image first
        private static readonly int[] InventoryCounts =
        {
            100, 100, 100, 27, 3, 62, 3, 2, 52, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 35, 63, 100, 100, 100, 100, 100,
            100, 100, 53, 23, 11, 100, 100, 100, 100, 92,
            100, 100, 32, 100, 8,
        };

        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "SlotGrid";

        public VisualElement CreateSample()
        {
            var root = SlotGallerySampleSheet.CreateSampleRoot();
            var icon = SlotGallerySampleSheet.Icon("1", "Iron Plate");

            // 持ち物（4-player-inventory）
            // Inventory (4-player-inventory)
            var inventoryRow = SlotGallerySampleSheet.AddRow(root, "slot-gallery__inventory");
            var inventory = new SlotGrid("gallery-slot-grid-inventory", 10);
            for (var i = 0; i < InventoryCounts.Length; i++)
            {
                var slot = new ItemSlot($"gallery-inventory-{i}");
                slot.SetContent(new ItemSlotContent(0 < InventoryCounts[i] ? icon : null, InventoryCounts[i], new ItemSlotMarks(false, false, false), null, null, null));
                inventory.AddItem(slot);
            }
            inventoryRow.Add(inventory);

            // クラフトレシピの一覧（4-player-inventory の右）: 未所持のカタログ 6 列 3 行
            // Craft recipe list (right of 4-player-inventory): unowned catalog, 6 columns by 3 rows
            var catalogRow = SlotGallerySampleSheet.AddRow(root, "slot-gallery__item-list");
            var catalog = new SlotGrid("gallery-slot-grid-catalog", 6);
            for (var i = 0; i < 18; i++)
            {
                var slot = new ItemSlot($"gallery-catalog-{i}");
                slot.SetContent(new ItemSlotContent(icon, 0, new ItemSlotMarks(false, true, false), null, null, null));
                catalog.AddItem(slot);
            }
            catalogRow.Add(catalog);
            return root;
        }
    }
}
