using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory.ItemManaged
{
    public class EquipmentInventoryData : IOpenableInventory
    {
        private readonly int _playerId;
        private readonly EquipmentInventoryUpdateEvent _equipmentInventoryUpdateEvent;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;

        public EquipmentInventoryData(int playerId, EquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent,
            ItemStackFactory itemStackFactory)
        {
            _playerId = playerId;
            _equipmentInventoryUpdateEvent = equipmentInventoryUpdateEvent;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent,
                itemStackFactory, 1);
        }
        public EquipmentInventoryData(int playerId, EquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent, ItemStackFactory itemStackFactory,IItemStack itemStacks) : 
            this(playerId, equipmentInventoryUpdateEvent, itemStackFactory)
        {
            _openableInventoryService.SetItemWithoutEvent(0,itemStacks);
        }

        

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _equipmentInventoryUpdateEvent.OnInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(
                _playerId,slot,itemStack));
        }
        
        public IItemStack GetItem(int slot) { return _openableInventoryService.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _openableInventoryService.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _openableInventoryService.SetItem(slot, itemId,count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _openableInventoryService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _openableInventoryService.ReplaceItem(slot, itemId,count); }
        public IItemStack InsertItem(IItemStack itemStack) { return _openableInventoryService.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _openableInventoryService.InsertItem(itemId,count); }
        public int GetSlotSize() { return _openableInventoryService.GetSlotSize(); }
    }
}