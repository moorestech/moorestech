using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item;
using Core.Update;
using Game.Block.Interface;
using Game.Block.BlockInventory;
using Game.Block.Blocks.Service;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks.Chest
{
    public class VanillaChest : IBlock, IBlockInventory, IOpenableInventory
    {
        public IBlockComponentManager ComponentManager { get; } = new BlockComponentManager();
        public IObservable<ChangedBlockState> BlockStateChange => _onBlockStateChange;
        private readonly Subject<ChangedBlockState> _onBlockStateChange = new();

        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;

        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        public VanillaChest(int blockId, int entityId, long blockHash, int slotNum, ItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate)
        {
            BlockId = blockId;
            EntityId = entityId;
            _blockInventoryUpdate = blockInventoryUpdate;
            BlockHash = blockHash;

            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, slotNum);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        public VanillaChest(string saveData, int blockId, int entityId, long blockHash, int slotNum,
            ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent blockInventoryUpdate) : this(blockId,
            entityId, blockHash, slotNum, itemStackFactory, blockInventoryUpdate)
        {
            var split = saveData.Split(',');
            for (var i = 0; i < split.Length; i += 2)
            {
                var itemHash = long.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                var item = itemStackFactory.Create(itemHash, itemCount);
                _itemDataStoreService.SetItem(i / 2, item);
            }
        }

        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }

        public string GetSaveState()
        {
            //itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = "";
            foreach (var itemStack in _itemDataStoreService.Inventory)
                saveState += $"{itemStack.ItemHash},{itemStack.Count},";
            return saveState.TrimEnd(',');
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            _connectInventory.Add(blockInventory);
            //NullInventoryは削除しておく
            for (var i = _connectInventory.Count - 1; i >= 0; i--)
                if (_connectInventory[i] is NullIBlockInventory)
                    _connectInventory.RemoveAt(i);
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            _connectInventory.Remove(blockInventory);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _itemDataStoreService.InsertItem(itemStack);
        }

        public int GetSlotSize()
        {
            return _itemDataStoreService.GetSlotSize();
        }

        public IItemStack GetItem(int slot)
        {
            return _itemDataStoreService.GetItem(slot);
        }


        public ReadOnlyCollection<IItemStack> Items => _itemDataStoreService.Items;

        public void SetItem(int slot, int itemId, int count)
        {
            _itemDataStoreService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            return _itemDataStoreService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _itemDataStoreService.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }


        private void Update()
        {
            for (var i = 0; i < _itemDataStoreService.Inventory.Count; i++)
                _itemDataStoreService.SetItem(i,
                    _connectInventoryService.InsertItem(_itemDataStoreService.Inventory[i]));
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                EntityId, slot, itemStack));
        }
    }
}