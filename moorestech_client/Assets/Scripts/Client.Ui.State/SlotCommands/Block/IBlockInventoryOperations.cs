using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロック側（サブインベントリ）の枠の口。プレイヤー側の IPlayerInventoryOperations の対
    /// Port to the block-side (sub-inventory) slots; the counterpart of the player-side IPlayerInventoryOperations
    /// </summary>
    public interface IBlockInventoryOperations
    {
        bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots);
        PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count);
        void CollectToGrab();
        void CollectToBlockSlot(int blockSlot);
    }

    // ブロックの枠の番号の範囲判定（実行器とコマンドが共有する 1 か所）
    // Range check for block slot indices (the single place shared by the executor and the commands)
    public static class BlockSlotIndex
    {
        public static bool IsInRange(IReadOnlyList<SlotItem> slots, int blockSlot)
        {
            return 0 <= blockSlot && blockSlot < slots.Count;
        }
    }
}
