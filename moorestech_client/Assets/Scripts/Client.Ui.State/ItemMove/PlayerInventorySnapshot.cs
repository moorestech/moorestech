using System.Collections.Generic;
using Client.Ui.State.Items;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// 操作の瞬間に読んだプレイヤーの持ち物。描画時の値を混ぜないため計画の入力はすべてここから取る
    /// The player's inventory read at the moment of the gesture; every planner input comes from here, never from render-time values
    /// </summary>
    public sealed class PlayerInventorySnapshot
    {
        public IReadOnlyList<SlotItem> MainSlots { get; }
        public SlotItem Grab { get; }
        public IReadOnlyList<SlotItem> Equipment { get; }

        public PlayerInventorySnapshot(IReadOnlyList<SlotItem> mainSlots, SlotItem grab, IReadOnlyList<SlotItem> equipment)
        {
            MainSlots = mainSlots;
            Grab = grab;
            Equipment = equipment;
        }

        // 枠数が縮んだ直後の古い番地は対象が無いので false を返す
        // A stale ref right after the slot count shrinks has no target, so return false
        public bool TryResolveSlot(ItemMoveSlotRef target, out SlotItem slot)
        {
            switch (target.Area)
            {
                case ItemMoveArea.Grab:
                    slot = Grab;
                    return true;
                case ItemMoveArea.Equipment:
                    return TryGetAt(Equipment, target.Slot, out slot);
                case ItemMoveArea.Main:
                    return TryGetAt(MainSlots, target.Slot, out slot);
                default:
                    // プレイヤー側の操作に block の番地は来ない
                    // Player-side gestures never carry a block ref
                    slot = default;
                    return false;
            }
        }

        private static bool TryGetAt(IReadOnlyList<SlotItem> slots, int index, out SlotItem slot)
        {
            var inRange = 0 <= index && index < slots.Count;
            slot = inRange ? slots[index] : default;
            return inRange;
        }
    }
}
