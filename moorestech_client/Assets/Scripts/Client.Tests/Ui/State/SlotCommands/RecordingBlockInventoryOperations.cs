using System.Collections.Generic;
using Core.Master;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// ブロック側の口の記録係。閉じた状態と移動の結果を差し替えられる
    /// Recorder for the block-side port; the closed state and move outcomes can be replaced
    /// </summary>
    public sealed class RecordingBlockInventoryOperations : IBlockInventoryOperations
    {
        public readonly List<(ItemMoveSlotRef From, ItemMoveSlotRef To, int Count)> Moves = new();
        public readonly List<string> Collects = new();
        public SlotItem[] Slots = new SlotItem[0];
        public bool Open = true;
        public PlayerItemMoveOutcome NextOutcome = PlayerItemMoveOutcome.Moved;
        public bool WriteThrough;              // 真なら移動を Slots に即時に書く（コントローラの先書きの再現。D-37）
        public ItemId WriteThroughItemId;      // 先書きで枠へ入るアイテム

        public bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots)
        {
            slots = Slots;
            return Open;
        }

        public PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            Moves.Add((from, to, count));
            if (WriteThrough) ApplyToSlots(from, to, count);
            return NextOutcome;
        }

        // ブロックの枠の増減だけを書く（grab・main 側は呼び手の記録係が持つ）
        // Writes only the block-slot side (grab/main are owned by the caller's recorder)
        private void ApplyToSlots(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            var copy = (SlotItem[])Slots.Clone();
            if (from.Area == ItemMoveArea.Block) copy[from.Slot] = new SlotItem(copy[from.Slot].ItemId, copy[from.Slot].Count - count);
            if (to.Area == ItemMoveArea.Block) copy[to.Slot] = new SlotItem(WriteThroughItemId, copy[to.Slot].Count + count);
            Slots = copy;
        }

        public void CollectToGrab() => Collects.Add("grab");
        public void CollectToBlockSlot(int blockSlot) => Collects.Add($"block:{blockSlot}");
    }
}
