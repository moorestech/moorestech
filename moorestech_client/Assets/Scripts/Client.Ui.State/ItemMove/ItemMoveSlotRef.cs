using System;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// スロットの置き場。Web の SlotRef.area の写し
    /// Where a slot lives; a port of the Web SlotRef.area
    /// </summary>
    public enum ItemMoveArea
    {
        Main,
        Equipment,
        Grab,
        Block,
    }

    /// <summary>
    /// 置き場と番号でスロットを指す値。Web の SlotRef の写し
    /// A value addressing a slot by area and index; a port of the Web SlotRef
    /// </summary>
    public readonly struct ItemMoveSlotRef : IEquatable<ItemMoveSlotRef>
    {
        // 持ち手は 1 枠なので番号は常に 0
        // The grab is a single slot, so its index is always 0
        public static readonly ItemMoveSlotRef Grab = new(ItemMoveArea.Grab, 0);

        public readonly ItemMoveArea Area;
        public readonly int Slot;

        public ItemMoveSlotRef(ItemMoveArea area, int slot)
        {
            Area = area;
            Slot = slot;
        }

        public static ItemMoveSlotRef Main(int slot) => new(ItemMoveArea.Main, slot);
        public static ItemMoveSlotRef Equipment(int slot) => new(ItemMoveArea.Equipment, slot);
        public static ItemMoveSlotRef Block(int slot) => new(ItemMoveArea.Block, slot);

        public bool Equals(ItemMoveSlotRef other) => Area == other.Area && Slot == other.Slot;
        public override bool Equals(object obj) => obj is ItemMoveSlotRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Area, Slot);
        public static bool operator ==(ItemMoveSlotRef left, ItemMoveSlotRef right) => left.Equals(right);
        public static bool operator !=(ItemMoveSlotRef left, ItemMoveSlotRef right) => !left.Equals(right);
        public override string ToString() => $"{Area}:{Slot}";
    }
}
