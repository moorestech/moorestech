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
        private readonly int _blockId;
        private readonly int _intId;
        private readonly int _requestPower;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly List<IItemStack> _outputSlot;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;

        private int _defaultMiningTime = int.MaxValue;
        private int _miningItemId = ItemConst.EmptyItemId;
        
        private int _nowPower = 0;
        private double _remainingMillSecond = int.MaxValue;

        public VanillaMiner(int blockId, int intId, int requestPower, int outputSlot,
            ItemStackFactory itemStackFactory)
        {
            _blockId = blockId;
            _intId = intId;
            _requestPower = requestPower;
            _itemStackFactory = itemStackFactory;
            _outputSlot = CreateEmptyItemStacksList.Create(outputSlot, itemStackFactory);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdate.AddUpdateObject(this);
        }
        public VanillaMiner(string saveData,int blockId, int intId, int requestPower, int miningTime ,
            ItemStackFactory itemStackFactory)
        {
            _blockId = blockId;
            _intId = intId;
            _requestPower = requestPower;
            _remainingMillSecond = miningTime;
            _itemStackFactory = itemStackFactory;
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdate.AddUpdateObject(this);

            _outputSlot = new List<IItemStack>();
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var split = saveData.Split(',');
            _remainingMillSecond = int.Parse(split[0]);
            for (var i = 1; i < split.Length; i += 2)
            {
                var itemId = int.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                _outputSlot.Add(_itemStackFactory.Create(itemId, itemCount));
            }
        }

        public void Update()
        {
            _remainingMillSecond -= GameUpdate.UpdateTime * (_nowPower / (double) _requestPower);

            if (_remainingMillSecond <= 0)
            {
                _remainingMillSecond = _defaultMiningTime;

                //空きスロットを探索し、あるならアイテムを挿入
                var addItem = _itemStackFactory.Create(_miningItemId, 1);
                for (int i = 0; i < _outputSlot.Count; i++)
                {
                    if (!_outputSlot[i].IsAllowedToAdd(addItem)) continue;
                    //空きスロットに掘ったアイテムを挿入
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
                saveState += $",{itemStack.Id},{itemStack.Count}";
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

        public int GetIntId()
        {
            return _intId;
        }

        public int GetBlockId()
        {
            return _blockId;
        }

        public int GetRequestPower()
        {
            return _requestPower;
        }

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
        
        public IItemStack GetItem(int slot) { return _outputSlot[slot]; }
        public void SetItem(int slot, IItemStack itemStack) { _outputSlot[slot] = itemStack; }
        public int GetSlotSize() { return _outputSlot.Count; }
    }
}