using System;

namespace Game.PlayerInventory.Interface
{
    public static class PlayerInventoryConst
    {
        public const int MainInventoryColumns = 9;
        public const int MainInventoryRows = 5;
        public const int MainInventorySize = MainInventoryColumns * MainInventoryRows;

        public const int CraftingInventoryColumns = 3;
        public const int CraftingInventoryRows = 3;
        public const int CraftingSlotSize = CraftingInventoryColumns * CraftingInventoryRows;


        ///     

        public static readonly int[] HotBarSlots =
        {
            HotBarSlotToInventorySlot(0),
            HotBarSlotToInventorySlot(1),
            HotBarSlotToInventorySlot(2),
            HotBarSlotToInventorySlot(3),
            HotBarSlotToInventorySlot(4),
            HotBarSlotToInventorySlot(5),
            HotBarSlotToInventorySlot(6),
            HotBarSlotToInventorySlot(7),
            HotBarSlotToInventorySlot(8)
        };



        ///     0〜8ID

        /// <exception cref="Exception">0〜8</exception>
        public static int HotBarSlotToInventorySlot(int slot)
        {
            if (slot < 0 || MainInventoryColumns <= slot)
                throw new Exception("0～8");
            
            return (MainInventoryRows - 1) * MainInventoryColumns + slot;
        }
    }
}