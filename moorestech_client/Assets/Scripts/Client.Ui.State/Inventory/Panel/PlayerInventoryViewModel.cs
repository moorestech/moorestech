using System.Collections.Generic;
using Client.Ui.State.Items;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物パネルと装備 HUD が描く値（不変）
    /// Immutable values the inventory panel and equipment HUD draw
    /// </summary>
    public sealed class PlayerInventoryViewModel
    {
        public IReadOnlyList<SlotItem> MainSlots { get; }
        public IReadOnlyList<SlotItem> EquipmentSlots { get; }
        public int SelectedEquipment { get; }
        // スロット番号 → 所持品の GUID（小文字）。アンカー id は View が組む
        // Slot index -> owned item GUID (lowercase); the view builds the anchor id
        public IReadOnlyDictionary<int, string> MainSlotItemGuids { get; }

        public PlayerInventoryViewModel(IReadOnlyList<SlotItem> mainSlots, IReadOnlyList<SlotItem> equipmentSlots, int selectedEquipment, IReadOnlyDictionary<int, string> mainSlotItemGuids)
        {
            MainSlots = mainSlots;
            EquipmentSlots = equipmentSlots;
            SelectedEquipment = selectedEquipment;
            MainSlotItemGuids = mainSlotItemGuids;
        }
    }
}
