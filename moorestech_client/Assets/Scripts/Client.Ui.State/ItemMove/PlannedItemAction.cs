using System;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// 計画された操作の種類。Web の action 名（inventory.move_item 等）と 1 対 1
    /// Kind of planned action; one-to-one with the Web action names (inventory.move_item etc.)
    /// </summary>
    public enum PlannedItemActionKind
    {
        InventoryMoveItem,
        InventorySplit,
        InventoryCollect,
        BlockInventoryMoveItem,
        BlockInventorySplit,
        BlockInventoryCollect,
    }

    /// <summary>
    /// 送るべき操作 1 つ。Web の PlannedAction の写しで、型付きコマンドへの配線は S4-alpha が持つ
    /// One action to send; a port of the Web PlannedAction, wired to typed commands by S4-alpha
    /// </summary>
    public readonly struct PlannedItemAction : IEquatable<PlannedItemAction>
    {
        public readonly PlannedItemActionKind Kind;

        // 移動・分割の起点。収集ではクリックしたスロット
        // Origin of a move or split; the clicked slot for a collect
        public readonly ItemMoveSlotRef From;

        // 移動の行き先と個数。分割・収集では使わない（既定値と 0）
        // Destination and count of a move; unused (default and 0) for split and collect
        public readonly ItemMoveSlotRef To;
        public readonly int Count;

        private PlannedItemAction(PlannedItemActionKind kind, ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            Kind = kind;
            From = from;
            To = to;
            Count = count;
        }

        public static PlannedItemAction InventoryMoveItem(ItemMoveSlotRef from, ItemMoveSlotRef to, int count) => new(PlannedItemActionKind.InventoryMoveItem, from, to, count);
        public static PlannedItemAction InventorySplit(ItemMoveSlotRef from) => new(PlannedItemActionKind.InventorySplit, from, default, 0);
        public static PlannedItemAction InventoryCollect(ItemMoveSlotRef slot) => new(PlannedItemActionKind.InventoryCollect, slot, default, 0);
        public static PlannedItemAction BlockInventoryMoveItem(ItemMoveSlotRef from, ItemMoveSlotRef to, int count) => new(PlannedItemActionKind.BlockInventoryMoveItem, from, to, count);
        public static PlannedItemAction BlockInventorySplit(ItemMoveSlotRef from) => new(PlannedItemActionKind.BlockInventorySplit, from, default, 0);
        public static PlannedItemAction BlockInventoryCollect(ItemMoveSlotRef slot) => new(PlannedItemActionKind.BlockInventoryCollect, slot, default, 0);

        // 片側がブロックなら block_inventory.move_item、両側プレイヤーなら inventory.move_item（Web の action の選び分け）
        // block_inventory.move_item when either side is the block, inventory.move_item otherwise (the Web's action choice)
        internal static PlannedItemAction MoveItem(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            var viaBlock = from.Area == ItemMoveArea.Block || to.Area == ItemMoveArea.Block;
            return viaBlock ? BlockInventoryMoveItem(from, to, count) : InventoryMoveItem(from, to, count);
        }

        public bool Equals(PlannedItemAction other) => Kind == other.Kind && From == other.From && To == other.To && Count == other.Count;
        public override bool Equals(object obj) => obj is PlannedItemAction other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, From, To, Count);
        public override string ToString() => $"{Kind}({From}->{To} x{Count})";
    }
}
