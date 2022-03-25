using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Service;
using Core.Block.Event;
using Core.Inventory;
using Core.Item;
using Core.Update;

namespace Core.Block.Blocks.Chest.InventoryController
{
    public class VanillaChestBlockInventory : IUpdate,IOpenableInventory
    {
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly int _entityId;

        private readonly int _inputSlotSize;
        
        
        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.Inventory;

        public VanillaChestBlockInventory(int SlotNum, ItemStackFactory itemStackFactory,BlockOpenableInventoryUpdateEvent blockInventoryUpdate,int entityId)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _entityId = entityId;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,SlotNum);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdate.AddUpdateObject(this);
        }
        
        void InsertConnectInventory()
        {
            for (int i = 0; i < OutputSlot.Count; i++)
            {
                _itemDataStoreService.SetItem(i,_connectInventoryService.InsertItem(OutputSlot[i]));
            }
        }
        
        
        public void AddConnectInventory(IBlockInventory blockInventory)
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
        
        
        public void RemoveConnectInventory(IBlockInventory blockInventory) { _connectInventory.Remove(blockInventory); }


        public void Update()
        {
            InsertConnectInventory(); 
        }
        
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot,itemStack);
        }

        public void SetItem(int slot, int itemId, int count)
        {
            _itemDataStoreService.SetItem(slot,itemId,count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _itemDataStoreService.InsertItem(itemStack);
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            return _itemDataStoreService.InsertItem(itemId, count);
        }

        public int GetSlotSize()
        {
            throw new System.NotImplementedException();
        }

        public IItemStack GetItem(int slot)
        {
            return _itemDataStoreService.GetItem(slot);
        }
        

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _entityId, slot + _inputSlotSize, itemStack));
        }
        
        
        
    }
}