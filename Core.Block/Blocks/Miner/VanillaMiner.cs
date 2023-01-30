using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Service;
using Core.Block.Event;
using Core.Const;
using Core.Electric;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.Blocks.Miner
{
    public class VanillaMiner : IBlock, IBlockElectric, IBlockInventory, IUpdate,IMiner,IOpenableInventory
    {

        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        
        public int RequestPower  { get; }
        private readonly ItemStackFactory _itemStackFactory;
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly OpenableInventoryItemDataStoreService _openableInventoryItemDataStoreService;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;

        private int _defaultMiningTime = int.MaxValue;
        private int _miningItemId = ItemConst.EmptyItemId;
        
        private int _nowPower = 0;
        private int _remainingMillSecond = int.MaxValue;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;

        public VanillaMiner(int blockId, int entityId, ulong blockHash, int requestPower, int outputSlotCount, ItemStackFactory itemStackFactory,BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent)
        {
            BlockId = blockId;
            EntityId = entityId;
            RequestPower = requestPower;
            BlockHash = blockHash;
            
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdate = openableInventoryUpdateEvent;
            
            _openableInventoryItemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,outputSlotCount);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            
            GameUpdate.AddUpdateObject(this);
        }
        public VanillaMiner(string saveData,int blockId, int entityId, ulong blockHash, int requestPower,int outputSlotCount, ItemStackFactory itemStackFactory,BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent)
            :this(blockId, entityId, blockHash, requestPower, outputSlotCount, itemStackFactory,openableInventoryUpdateEvent)
        {
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var split = saveData.Split(',');
            _remainingMillSecond = int.Parse(split[0]);
            var inventoryItems = new List<IItemStack>();
            for (var i = 1; i < split.Length; i += 2)
            {
                var itemHash = ulong.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                inventoryItems.Add(_itemStackFactory.Create(itemHash, itemCount));
            }
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                _openableInventoryItemDataStoreService.SetItem(i,inventoryItems[i]);
            }
        }

        public void Update()
        {
            var subTime = (int)(RequestPower == 0 ? GameUpdate.UpdateMillSecondTime : GameUpdate.UpdateMillSecondTime * (_nowPower / (double) RequestPower));
            if (subTime <= 0)
            {
                return;
            }
            _remainingMillSecond -= subTime;

            if (_remainingMillSecond <= 0)
            {
                _remainingMillSecond = _defaultMiningTime;

                //空きスロットを探索し、あるならアイテムを挿入
                var addItem = _itemStackFactory.Create(_miningItemId, 1);
                _openableInventoryItemDataStoreService.InsertItem(addItem);
            }

            _nowPower = 0;
            InsertConnectInventory();
        }

        void InsertConnectInventory()
        {
            for (int i = 0; i < _openableInventoryItemDataStoreService.Items.Count; i++)
            {
                var insertedItem = _connectInventoryService.InsertItem(_openableInventoryItemDataStoreService.Items[i]);
                _openableInventoryItemDataStoreService.SetItem(i,insertedItem);
            }
        }

        public string GetSaveState()
        {
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = $"{_remainingMillSecond}";
            foreach (var itemStack in _openableInventoryItemDataStoreService.Items)
            {
                saveState += $",{itemStack.ItemHash},{itemStack.Count}";
            }

            return saveState;
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            _connectInventory.Add(blockInventory);
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            _connectInventory.Remove(blockInventory);
        }


        #region Implimantion IOpenableInventory

        public ReadOnlyCollection<IItemStack> Items => _openableInventoryItemDataStoreService.Items;
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemId, count); }

        public IItemStack InsertItem(IItemStack itemStack) { return _openableInventoryItemDataStoreService.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _openableInventoryItemDataStoreService.InsertItem(itemId, count); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { return _openableInventoryItemDataStoreService.InsertItem(itemStacks); }
        public bool InsertionCheck(List<IItemStack> itemStacks) { return _openableInventoryItemDataStoreService.InsertionCheck(itemStacks); }
        public void SetItem(int slot, int itemId, int count) { _openableInventoryItemDataStoreService.SetItem(slot,itemId,count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemStack); }

        #endregion
        
        private void InvokeEvent(int slot, IItemStack itemStack) { _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(EntityId, slot, itemStack)); }


        public void SupplyPower(int power)
        {
            _nowPower = power;
        }

        public void SetMiningItem(int miningItemId, int miningTime)
        {
            if (_defaultMiningTime != int.MaxValue)
            {
                throw new Exception("採掘機に鉱石の設定をできるのは1度だけです");
            }
            _miningItemId = miningItemId;
            _defaultMiningTime = miningTime;
            _remainingMillSecond = _defaultMiningTime;
        }

        public IItemStack GetItem(int slot) { return _openableInventoryItemDataStoreService.GetItem(slot); }

        public void SetItem(int slot, IItemStack itemStack) { _openableInventoryItemDataStoreService.SetItem(slot, itemStack); }
        public int GetSlotSize() { return _openableInventoryItemDataStoreService.GetSlotSize(); }
    }
}