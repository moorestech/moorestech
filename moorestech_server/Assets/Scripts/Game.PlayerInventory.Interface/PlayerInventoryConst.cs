using System;

namespace Game.PlayerInventory.Interface
{
    public static class PlayerInventoryConst
    {
        public const int MainInventoryColumns = 9;

        public const int HotBarSlotCount = 9;

        // ホットバーは常にメインインベントリの最後の9スロット
        // The hotbar is always the last nine slots of the main inventory
        public static int HotBarSlotToInventorySlot(int hotBarSlot, int mainInventorySize)
        {
            if (hotBarSlot < 0 || HotBarSlotCount <= hotBarSlot)
                throw new Exception("ホットバーは0～8までです");
            return mainInventorySize - HotBarSlotCount + hotBarSlot;
        }

        public static int[] GetHotBarSlots(int mainInventorySize)
        {
            var slots = new int[HotBarSlotCount];
            for (var i = 0; i < HotBarSlotCount; i++) slots[i] = mainInventorySize - HotBarSlotCount + i;
            return slots;
        }

        public static bool IsHotBarSlot(int slot, int mainInventorySize)
        {
            return mainInventorySize - HotBarSlotCount <= slot && slot < mainInventorySize;
        }
    }
}
