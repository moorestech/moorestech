using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Game.Context;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;

namespace Game.PlayerInventory.ItemManaged
{
    public class MainOpenableInventoryData : IOpenableInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems => _openableInventoryService.InventoryItems;
        
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;
        private readonly int _playerId;
        
        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent)
        {
            _playerId = playerId;
            _mainInventoryUpdateEvent = mainInventoryUpdateEvent;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, PlayerInventoryConst.MainInventorySize);
        }
        
        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent, List<IItemStack> itemStacks) : this(playerId, mainInventoryUpdateEvent)
        {
            for (var i = 0; i < itemStacks.Count; i++) _openableInventoryService.SetItemWithoutEvent(i, itemStacks[i]);
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
        
        /// <summary>
        ///     プレイヤーのメインインベントリの場合はホットバーを優先的にInsertする
        /// </summary>
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _openableInventoryService.InsertItemWithPrioritySlot(itemStack, PlayerInventoryConst.HotBarSlots);
        }
        
        public IItemStack InsertItem(int itemId, int count)
        {
            return _openableInventoryService.InsertItemWithPrioritySlot(itemId, count,
                PlayerInventoryConst.HotBarSlots);
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
            _mainInventoryUpdateEvent.OnInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(
                _playerId, slot, itemStack));
        }
    }
}