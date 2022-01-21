using System;

namespace Game.PlayerInventory.Interface
{
    public static class PlayerInventoryConst
    {
        public const int MainInventoryColumns = 9;
        public const int MainInventoryRows = 5;
        public const int MainInventorySize = MainInventoryColumns * MainInventoryRows;

        public static int HotBarSlotToInventorySlot(int slot)
        {
            if (slot < 0 || MainInventoryColumns <= slot)
                throw new Exception("ホットバーは0～8までです");
            //インベントリの一番したがホットバーとなる
            return (MainInventoryColumns-1) * MainInventoryRows + slot;
        }
    }
}