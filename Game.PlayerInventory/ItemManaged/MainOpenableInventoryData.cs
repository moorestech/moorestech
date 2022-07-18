using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory.ItemManaged
{
    public class MainOpenableInventoryData : IOpenableInventory
    {
        private readonly int _playerId;
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;

        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent,
            ItemStackFactory itemStackFactory)
        {
            _playerId = playerId;
            _mainInventoryUpdateEvent = mainInventoryUpdateEvent;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent,
                itemStackFactory, PlayerInventoryConst.MainInventorySize);
        }
        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent, ItemStackFactory itemStackFactory,List<IItemStack> itemStacks) : 
            this(playerId, mainInventoryUpdateEvent, itemStackFactory)
        {
            for (int i = 0; i < itemStacks.Count; i++)
            {
                _openableInventoryService.SetItemWithoutEvent(i,itemStacks[i]);
            }
        }

        

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _mainInventoryUpdateEvent.OnInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(
                _playerId,slot,itemStack));
        }
        
        public ReadOnlyCollection<IItemStack> Items => _openableInventoryService.Items;
        public IItemStack GetItem(int slot) { return _openableInventoryService.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _openableInventoryService.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _openableInventoryService.SetItem(slot, itemId,count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _openableInventoryService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _openableInventoryService.ReplaceItem(slot, itemId,count); }
        public IItemStack InsertItem(IItemStack itemStack) { return _openableInventoryService.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _openableInventoryService.InsertItem(itemId,count); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { return _openableInventoryService.InsertItem(itemStacks); }
        public bool InsertionCheck(List<IItemStack> itemStacks) { return _openableInventoryService.InsertionCheck(itemStacks); }

        public int GetSlotSize() { return _openableInventoryService.GetSlotSize(); }
    }
}