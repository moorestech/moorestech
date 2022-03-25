using System;
using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Service;
using Core.Block.Event;
using Core.Inventory;
using Core.Item;
using Core.Update;

namespace Core.Block.Blocks.Chest
{
    
    public class VanillaChest: IBlock, IBlockInventory, IOpenableInventory,IUpdate
    {
        private readonly int _entityId;
        private readonly int _blockId;
        
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly int _slotSize;

        public VanillaChest(int blockId,int entityId,int slotNum, ItemStackFactory itemStackFactory,BlockOpenableInventoryUpdateEvent blockInventoryUpdate)
        {
            _slotSize = slotNum;
            _blockInventoryUpdate = blockInventoryUpdate;
            _entityId = entityId;
            _blockId = blockId;
            
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,slotNum);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdate.AddUpdateObject(this);
        }

        public VanillaChest(string saveData,int blockId,int entityId,int slotNum, ItemStackFactory itemStackFactory,BlockOpenableInventoryUpdateEvent blockInventoryUpdate) : this( blockId,entityId,slotNum,  itemStackFactory,blockInventoryUpdate)
        {
            var split = saveData.Split(',');
            Console.WriteLine(saveData);
            for (var i = 0; i < split.Length; i += 2)
            {
                var itemId = int.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                var item = itemStackFactory.Create(itemId, itemCount);
                _itemDataStoreService.SetItem(i/2, item);
            }
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _entityId, slot + _slotSize, itemStack));
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            _connectInventory.Add(blockInventory);
            //NullInventoryは削除しておく
            for (int i = _connectInventory.Count - 1; i >= 0; i--)
            {
                if (_connectInventory[i] is NullIBlockInventory)
                {
                    _connectInventory.RemoveAt(i);
                }
            }
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory) { _connectInventory.Remove(blockInventory); }


        public void Update()
        {
            for (int i = 0; i < _itemDataStoreService.Inventory.Count; i++)
            {
                _itemDataStoreService.SetItem(i,
                    _connectInventoryService.InsertItem(_itemDataStoreService.Inventory[i]));
            }
        }
        
        public string GetSaveState()
        {
            //itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = "";
            foreach (var itemStack in _itemDataStoreService.Inventory)
            {
                saveState += $"{itemStack.Id},{itemStack.Count},";
            }
            return saveState.TrimEnd(',');
        }
        
        
        public void SetItem(int slot, IItemStack itemStack) { _itemDataStoreService.SetItem(slot,itemStack); }

        public void SetItem(int slot, int itemId, int count) { _itemDataStoreService.SetItem(slot,itemId,count); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _itemDataStoreService.ReplaceItem(slot, itemStack); }

        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _itemDataStoreService.ReplaceItem(slot, itemId, count); }

        public IItemStack InsertItem(IItemStack itemStack) { return _itemDataStoreService.InsertItem(itemStack); }

        public IItemStack InsertItem(int itemId, int count) { return _itemDataStoreService.InsertItem(itemId, count); }

        public int GetSlotSize() { return _slotSize; }

        public IItemStack GetItem(int slot) { return _itemDataStoreService.GetItem(slot); }
        public int GetEntityId() { return _entityId; }

        public int GetBlockId() { return _blockId; }
    }
}