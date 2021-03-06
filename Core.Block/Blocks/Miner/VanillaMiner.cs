using System;
using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Service;
using Core.Block.Config.LoadConfig.Param;
using Core.Const;
using Core.Electric;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;
using Core.Ore;
using Core.Update;

namespace Core.Block.Blocks.Miner
{
    public class VanillaMiner : IBlock, IBlockElectric, IBlockInventory, IUpdate,IMiner
    {

        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        
        public int RequestPower  { get; }
        private readonly ItemStackFactory _itemStackFactory;
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly List<IItemStack> _outputSlot;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;

        private int _defaultMiningTime = int.MaxValue;
        private int _miningItemId = ItemConst.EmptyItemId;
        
        private int _nowPower = 0;
        private int _remainingMillSecond = int.MaxValue;

        public VanillaMiner(int blockId, int entityId, ulong blockHash, int requestPower, int outputSlot, ItemStackFactory itemStackFactory)
        {
            BlockId = blockId;
            EntityId = entityId;
            RequestPower = requestPower;
            _itemStackFactory = itemStackFactory;
            BlockHash = blockHash;
            _outputSlot = CreateEmptyItemStacksList.Create(outputSlot, itemStackFactory);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdate.AddUpdateObject(this);
        }
        public VanillaMiner(string saveData,int blockId, int entityId, ulong blockHash, int requestPower, int miningTime , ItemStackFactory itemStackFactory)
        {
            BlockId = blockId;
            EntityId = entityId;
            RequestPower = requestPower;
            _remainingMillSecond = miningTime;
            _itemStackFactory = itemStackFactory;
            BlockHash = blockHash;
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdate.AddUpdateObject(this);

            _outputSlot = new List<IItemStack>();
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var split = saveData.Split(',');
            _remainingMillSecond = int.Parse(split[0]);
            for (var i = 1; i < split.Length; i += 2)
            {
                var itemHash = ulong.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                _outputSlot.Add(_itemStackFactory.Create(itemHash, itemCount));
            }
        }

        public void Update()
        {
            var subTime = (int) (GameUpdate.UpdateTime * (_nowPower / (double) RequestPower));
            if (subTime <= 0)
            {
                return;
            }
            _remainingMillSecond -= subTime;

            if (_remainingMillSecond <= 0)
            {
                _remainingMillSecond = _defaultMiningTime;

                //??????????????????????????????????????????????????????????????????
                var addItem = _itemStackFactory.Create(_miningItemId, 1);
                for (int i = 0; i < _outputSlot.Count; i++)
                {
                    if (!_outputSlot[i].IsAllowedToAdd(addItem)) continue;
                    //???????????????????????????????????????????????????
                    _outputSlot[i] = _outputSlot[i].AddItem(addItem).ProcessResultItemStack;
                    break;
                }
            }

            _nowPower = 0;
            InsertConnectInventory();
        }

        void InsertConnectInventory()
        {
            for (int i = 0; i < _outputSlot.Count; i++)
            {
                _outputSlot[i] = _connectInventoryService.InsertItem(_outputSlot[i]);
            }
        }

        public string GetSaveState()
        {
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = $"{_remainingMillSecond}";
            foreach (var itemStack in _outputSlot)
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

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return itemStack;
        }

        public void SupplyPower(int power)
        {
            _nowPower = power;
        }

        public void SetMiningItem(int miningItemId, int miningTime)
        {
            if (_defaultMiningTime != int.MaxValue)
            {
                throw new Exception("?????????????????????????????????????????????1???????????????");
            }
            _miningItemId = miningItemId;
            _defaultMiningTime = miningTime;
            _remainingMillSecond = _defaultMiningTime;
        }
        
        public IItemStack GetItem(int slot) { return _outputSlot[slot]; }
        public void SetItem(int slot, IItemStack itemStack) { _outputSlot[slot] = itemStack; }
        public int GetSlotSize() { return _outputSlot.Count; }
    }
}