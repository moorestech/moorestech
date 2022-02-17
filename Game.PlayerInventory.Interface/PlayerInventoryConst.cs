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
        
        public const int CraftingOutputSlotSize = 1;
        public const int CraftingInventorySize = CraftingSlotSize + CraftingOutputSlotSize;
        

        public static int HotBarSlotToInventorySlot(int slot)
        {
            if (slot < 0 || MainInventoryColumns <= slot)
                throw new Exception("ホットバーは0～8までです");
            //インベントリの一番したがホットバーとなる
            return (MainInventoryRows-1) * MainInventoryColumns + slot;
        }
    }
}