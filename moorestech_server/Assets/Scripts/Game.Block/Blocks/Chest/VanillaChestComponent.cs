using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Chest
{
    public class VanillaChestComponent : IOpenableBlockInventoryComponent, IBlockSaveState
    {
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }
        
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        private readonly IDisposable _updateObservable;
        
        public VanillaChestComponent(BlockInstanceId blockInstanceId, int slotNum, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            BlockInstanceId = blockInstanceId;
            
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockConnectorComponent);
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotNum);
            
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public VanillaChestComponent(string saveData, BlockInstanceId blockInstanceId, int slotNum, BlockConnectorComponent<IBlockInventory> blockConnectorComponent) :
            this(blockInstanceId, slotNum, blockConnectorComponent)
        {
            var itemJsons = JsonConvert.DeserializeObject<List<ItemStackJsonObject>>(saveData);
            for (var i = 0; i < itemJsons.Count; i++)
            {
                var itemStack = itemJsons[i].ToItem();
                _itemDataStoreService.SetItem(i, itemStack);
            }
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            _itemDataStoreService.SetItem(slot, itemStack);
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemStack);
        }
        
        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.GetSlotSize();
        }
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return _itemDataStoreService.CreateCopiedItems();
        }
        
        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.GetItem(slot);
        }
        
        public void Destroy()
        {
            IsDestroy = true;
            _updateObservable.Dispose();
        }
        
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            var itemJson = new List<ItemStackJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems)
            {
                itemJson.Add(new ItemStackJsonObject(item));
            }
            
            return JsonConvert.SerializeObject(itemJson);
        }
        
        public void SetItem(int slot, int itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            _itemDataStoreService.SetItem(slot, itemId, count);
        }
        
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }
        
        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }
        
        public IItemStack InsertItem(int itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemId, count);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemStacks);
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }
        
        private void Update()
        {
            BlockException.CheckDestroy(this);
            
            for (var i = 0; i < _itemDataStoreService.InventoryItems.Count; i++)
            {
                var setItem = _connectInventoryService.InsertItem(_itemDataStoreService.InventoryItems[i]);
                _itemDataStoreService.SetItem(i, setItem);
            }
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }
    }
}