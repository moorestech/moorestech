using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IOpenableInventory GrabInventory;
        public readonly IOpenableInventory MainOpenableInventory;
        
        public PlayerInventoryData(IOpenableInventory mainOpenableInventory, IOpenableInventory grabInventory)
        {
            MainOpenableInventory = mainOpenableInventory;
            GrabInventory = grabInventory;
        }

        // 現在のサイズでホットバー番号解決
        // Resolve hotbar number using current size
        public int GetHotBarSlotIndex(int hotBarSlot)
        {
            return PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot, MainOpenableInventory.GetSlotSize());
        }
    }
}