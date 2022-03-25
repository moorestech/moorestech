using System;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Chest.InventoryController;
using Core.Block.Event;
using Core.Inventory;
using Core.Item;

namespace Core.Block.Blocks.Chest
{
    
    public class VanillaChest: IBlock, IBlockInventory, IOpenableInventory
    {
        private readonly int _blockId;
        private readonly int _entityId;
        private readonly VanillaChestBlockInventory _vanillaChestBlockInventory;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        
        public VanillaChest(int paramBlockId, int entityId, ItemStackFactory itemStackFactory,VanillaChestBlockInventory vanillaChestBlockInventory,BlockOpenableInventoryUpdateEvent blockInventoryUpdate,int chestChestItemNum)
        {
            _blockId = paramBlockId;
            _entityId = entityId;
            _vanillaChestBlockInventory = vanillaChestBlockInventory;
            _blockInventoryUpdate = blockInventoryUpdate;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, chestChestItemNum);
        }

        public VanillaChest(int paramBlockId, int entityId, string itemStackFactory, ItemStackFactory chestChestItemNum, int chestItemNum)
        {
            
        }


        public int GetEntityId()
        {
            return _entityId;
        }

        public int GetBlockId()
        {
            return _blockId;
        }

        public string GetSaveState()
        {
            throw new System.NotImplementedException();
        }

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            throw new System.NotImplementedException();
        }

        
        public IItemStack InsertItem(int itemId, int count)
        {
            throw new System.NotImplementedException();
        }

        int IOpenableInventory.GetSlotSize()
        {
            throw new System.NotImplementedException();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _itemDataStoreService.InsertItem(itemStack);
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            _vanillaChestBlockInventory.AddConnectInventory(blockInventory);
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            _vanillaChestBlockInventory.RemoveConnectInventory(blockInventory);
        }
        
        void IOpenableInventory.SetItem(int slot, IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        public void SetItem(int slot, int itemId, int count)
        {
            _itemDataStoreService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        public IItemStack GetItem(int slot)
        {

            return _itemDataStoreService.GetItem(slot);
        }

        void IBlockInventory.SetItem(int slot, IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        int IBlockInventory.GetSlotSize()
        {
            throw new System.NotImplementedException();
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _entityId,slot,itemStack));
        }
    }
}