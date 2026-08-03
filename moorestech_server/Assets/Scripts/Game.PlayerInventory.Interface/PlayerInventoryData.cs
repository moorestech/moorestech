using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IOpenableInventory GrabInventory;
        public readonly IOpenableInventory MainOpenableInventory;
        public readonly IEquipmentInventory EquipmentInventory;

        public PlayerInventoryData(IOpenableInventory mainOpenableInventory, IOpenableInventory grabInventory, IEquipmentInventory equipmentInventory)
        {
            MainOpenableInventory = mainOpenableInventory;
            GrabInventory = grabInventory;
            EquipmentInventory = equipmentInventory;
        }

        // 現在のサイズでホットバー番号解決
        // Resolve hotbar number using current size
        public int GetHotBarSlotIndex(int hotBarSlot)
        {
            return PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot, MainOpenableInventory.GetSlotSize());
        }
    }
}