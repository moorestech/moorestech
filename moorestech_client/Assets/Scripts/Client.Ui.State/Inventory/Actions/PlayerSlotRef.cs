using System;
using Client.Ui.State.ItemMove;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// プレイヤーがクリックできる枠（main・装備）の番地。grab とブロックは型の上で作れない（Web ホストの TryParseClickableSlotRef の範囲）
    /// Address of a player-clickable slot (main, equipment); grab and block cannot be built (range of the Web host's TryParseClickableSlotRef)
    /// </summary>
    public readonly struct PlayerSlotRef : IEquatable<PlayerSlotRef>
    {
        public readonly bool IsEquipment;
        public readonly int Slot;

        private PlayerSlotRef(bool isEquipment, int slot)
        {
            IsEquipment = isEquipment;
            Slot = slot;
        }

        public static PlayerSlotRef Main(int slot) => new(false, slot);
        public static PlayerSlotRef Equipment(int slot) => new(true, slot);

        // L0-s4a の番地からの境目。grab・ブロックは false（呼び手がブロック側へ回す）
        // Boundary from L0-s4a refs; grab and block give false (the caller routes them to the block side)
        public static bool TryFrom(ItemMoveSlotRef slot, out PlayerSlotRef playerSlot)
        {
            var isPlayerSlot = slot.Area == ItemMoveArea.Main || slot.Area == ItemMoveArea.Equipment;
            playerSlot = isPlayerSlot ? new PlayerSlotRef(slot.Area == ItemMoveArea.Equipment, slot.Slot) : default;
            return isPlayerSlot;
        }

        // L0-s4a の計画・スナップショットへ渡す番地
        // Ref passed to L0-s4a plans and snapshots
        public ItemMoveSlotRef ToItemMoveSlotRef() => IsEquipment ? ItemMoveSlotRef.Equipment(Slot) : ItemMoveSlotRef.Main(Slot);

        public bool Equals(PlayerSlotRef other) => IsEquipment == other.IsEquipment && Slot == other.Slot;
        public override bool Equals(object obj) => obj is PlayerSlotRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IsEquipment, Slot);
        public static bool operator ==(PlayerSlotRef left, PlayerSlotRef right) => left.Equals(right);
        public static bool operator !=(PlayerSlotRef left, PlayerSlotRef right) => !left.Equals(right);
        public override string ToString() => IsEquipment ? $"Equipment:{Slot}" : $"Main:{Slot}";
    }
}
