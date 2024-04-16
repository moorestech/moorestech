using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Component.IOConnector;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using Game.Context;
using UniRx;

namespace Game.Block.Blocks.Chest
{
    public class VanillaChestComponent : IBlockInventory, IOpenableBlockInventoryComponent, IBlockSaveState, IBlockStateChange
    {
        public int EntityId { get; }
        
        public bool IsDestroy { get; private set; }
        
        public IObservable<ChangedBlockState> BlockStateChange => _onBlockStateChange;
        private readonly Subject<ChangedBlockState> _onBlockStateChange = new();
        
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        public VanillaChestComponent(int entityId, int slotNum,BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            EntityId = entityId;

            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockConnectorComponent);

            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotNum);
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
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

        public string GetSaveState()
        {
            //itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = "";
            foreach (var itemStack in _itemDataStoreService.Inventory)
                saveState += $"{itemStack.ItemHash},{itemStack.Count},";
            return saveState.TrimEnd(',');
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
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(EntityId, slot, itemStack));
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}