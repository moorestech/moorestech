using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Game.Context;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface.Event;

namespace Game.PlayerInventory.ItemManaged
{
    public class GrabInventoryData : IOpenableInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems => _openableInventoryService.InventoryItems;
        
        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;
        private readonly int _playerId;
        
        public GrabInventoryData(int playerId, GrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            _playerId = playerId;
            _grabInventoryUpdateEvent = grabInventoryUpdateEvent;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, 1);
        }
        
        public GrabInventoryData(int playerId, GrabInventoryUpdateEvent grabInventoryUpdateEvent, IItemStack itemStacks) : this(playerId, grabInventoryUpdateEvent)
        {
            _openableInventoryService.SetItemWithoutEvent(0, itemStacks);
        }
        
        public IItemStack GetItem(int slot)
        {
            return _openableInventoryService.GetItem(slot);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            _openableInventoryService.SetItem(slot, itemStack);
        }
        
        public void SetItem(int slot, int itemId, int count)
        {
            _openableInventoryService.SetItem(slot, itemId, count);
        }
        
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return _openableInventoryService.ReplaceItem(slot, itemStack);
        }
        
        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            return _openableInventoryService.ReplaceItem(slot, itemId, count);
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _openableInventoryService.InsertItem(itemStack);
        }
        
        public IItemStack InsertItem(int itemId, int count)
        {
            return _openableInventoryService.InsertItem(itemId, count);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _openableInventoryService.InsertItem(itemStacks);
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _openableInventoryService.InsertionCheck(itemStacks);
        }
        
        public int GetSlotSize()
        {
            return _openableInventoryService.GetSlotSize();
        }
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return _openableInventoryService.CreateCopiedItems();
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _grabInventoryUpdateEvent.OnInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(
                _playerId, slot, itemStack));
        }
    }
}