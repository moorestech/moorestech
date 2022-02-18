using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using PlayerInventory.Event;

namespace PlayerInventory.ItemManaged
{
    public class MainInventoryData : IInventory
    {
        private readonly PlayerInventoryItemDataStoreService _inventoryService;

        public MainInventoryData(int playerId, PlayerMainInventoryUpdateEvent playerMainInventoryUpdateEvent,
            ItemStackFactory itemStackFactory)
        {
            _inventoryService = new PlayerInventoryItemDataStoreService(playerId, playerMainInventoryUpdateEvent,
                itemStackFactory, PlayerInventoryConst.MainInventorySize);
        }

        public IItemStack GetItem(int slot) { return _inventoryService.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _inventoryService.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _inventoryService.SetItem(slot, itemId,count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _inventoryService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _inventoryService.ReplaceItem(slot, itemId,count); }
        public IItemStack InsertItem(IItemStack itemStack) { return _inventoryService.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _inventoryService.InsertItem(itemId,count); }
        public int GetSlotSize() { return _inventoryService.GetSlotSize(); }
    }
}