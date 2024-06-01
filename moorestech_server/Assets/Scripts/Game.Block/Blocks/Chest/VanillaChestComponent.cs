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
using UniRx;

namespace Game.Block.Blocks.Chest
{
    public class VanillaChestComponent : IBlockInventory, IOpenableBlockInventoryComponent, IBlockSaveState
    {
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        private readonly IDisposable _updateObservable;
        
        public VanillaChestComponent(int entityId, int slotNum, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            EntityId = entityId;
            
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockConnectorComponent);
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotNum);
            
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public VanillaChestComponent(string saveData, int entityId, int slotNum, BlockConnectorComponent<IBlockInventory> blockConnectorComponent) :
            this(entityId, slotNum, blockConnectorComponent)
        {
            var split = saveData.Split(',');
            for (var i = 0; i < split.Length; i += 2)
            {
                var itemHash = long.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                var item = ServerContext.ItemStackFactory.Create(itemHash, itemCount);
                _itemDataStoreService.SetItem(i / 2, item);
            }
        }
        
        public int EntityId { get; }
        public bool IsDestroy { get; private set; }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            _itemDataStoreService.SetItem(slot, itemStack);
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            return _itemDataStoreService.InsertItem(itemStack);
        }
        
        public int GetSlotSize()
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            return _itemDataStoreService.GetSlotSize();
        }
        
        public IItemStack GetItem(int slot)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            return _itemDataStoreService.GetItem(slot);
        }
        
        public void Destroy()
        {
            IsDestroy = true;
            _updateObservable.Dispose();
        }
        
        public string GetSaveState()
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            //itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = "";
            foreach (var itemStack in _itemDataStoreService.Inventory)
                saveState += $"{itemStack.ItemHash},{itemStack.Count},";
            return saveState.TrimEnd(',');
        }
        
        
        public ReadOnlyCollection<IItemStack> Items => _itemDataStoreService.Items;
        
        public void SetItem(int slot, int itemId, int count)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            _itemDataStoreService.SetItem(slot, itemId, count);
        }
        
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }
        
        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }
        
        public IItemStack InsertItem(int itemId, int count)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            return _itemDataStoreService.InsertItem(itemId, count);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            return _itemDataStoreService.InsertItem(itemStacks);
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }
        
        private void Update()
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            for (var i = 0; i < _itemDataStoreService.Inventory.Count; i++)
                _itemDataStoreService.SetItem(i,
                    _connectInventoryService.InsertItem(_itemDataStoreService.Inventory[i]));
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(EntityId, slot, itemStack));
        }
    }
}