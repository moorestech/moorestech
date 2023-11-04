using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.EnergySystem;
using Core.Inventory;
using Core.Item;
using Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.Util;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Miner
{
    public abstract class VanillaMinerBase : IBlock, IEnergyConsumer, IBlockInventory, IUpdatable, IMiner, IOpenableInventory
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryItemDataStoreService;

        private int _currentPower;
        private VanillaMinerState _currentState = VanillaMinerState.Idle;

        private int _defaultMiningTime = int.MaxValue;

        private VanillaMinerState _lastMinerState;
        private List<IItemStack> _miningItems = new();
        private int _remainingMillSecond = int.MaxValue;

        protected VanillaMinerBase(int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent)
        {
            BlockId = blockId;
            EntityId = entityId;
            RequestEnergy = requestPower;
            BlockHash = blockHash;

            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdate = openableInventoryUpdateEvent;

            _openableInventoryItemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlotCount);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);

            GameUpdater.RegisterUpdater(this);
        }

        protected VanillaMinerBase(string saveData, int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent)
            : this(blockId, entityId, blockHash, requestPower, outputSlotCount, itemStackFactory, openableInventoryUpdateEvent)
        {
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var split = saveData.Split(',');
            _remainingMillSecond = int.Parse(split[0]);
            var inventoryItems = new List<IItemStack>();
            for (var i = 1; i < split.Length; i += 2)
            {
                var itemHash = long.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                inventoryItems.Add(_itemStackFactory.Create(itemHash, itemCount));
            }

            for (var i = 0; i < inventoryItems.Count; i++) _openableInventoryItemDataStoreService.SetItem(i, inventoryItems[i]);
        }

        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }
        public event Action<ChangedBlockState> OnBlockStateChange;

        public string GetSaveState()
        {
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = $"{_remainingMillSecond}";
            foreach (var itemStack in _openableInventoryItemDataStoreService.Items) saveState += $",{itemStack.ItemHash},{itemStack.Count}";

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

        public IItemStack GetItem(int slot)
        {
            return _openableInventoryItemDataStoreService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _openableInventoryItemDataStoreService.SetItem(slot, itemStack);
        }

        public int GetSlotSize()
        {
            return _openableInventoryItemDataStoreService.GetSlotSize();
        }

        public int RequestEnergy { get; }


        public void SupplyEnergy(int power)
        {
            _currentPower = power;
        }

        public void SetMiningItem(int miningItemId, int miningTime)
        {
            if (_defaultMiningTime != int.MaxValue) throw new Exception("採掘機に鉱石の設定をできるのは1度だけです");

            _miningItems = new List<IItemStack> { _itemStackFactory.Create(miningItemId, 1) };
            _defaultMiningTime = miningTime;
            _remainingMillSecond = _defaultMiningTime;
        }

        public void Update()
        {
            MinerProgressUpdate();
            CheckStateAndInvokeEventUpdate();
        }


        private void MinerProgressUpdate()
        {
            var subTime = MachineCurrentPowerToSubMillSecond.GetSubMillSecond(_currentPower, RequestEnergy);
            if (subTime <= 0)
            {
                //電力の都合で処理を進められないのでreturn
                _currentState = VanillaMinerState.Idle;
                return;
            }

            //insertできるかチェック
            if (!_openableInventoryItemDataStoreService.InsertionCheck(_miningItems))
            {
                //挿入できないのでreturn
                _currentState = VanillaMinerState.Idle;
                return;
            }

            _currentState = VanillaMinerState.Mining;

            _remainingMillSecond -= subTime;

            if (_remainingMillSecond <= 0)
            {
                _remainingMillSecond = _defaultMiningTime;

                //空きスロットを探索し、あるならアイテムを挿入
                _openableInventoryItemDataStoreService.InsertItem(_miningItems);
            }

            _currentPower = 0;
            InsertConnectInventory();
        }

        private void CheckStateAndInvokeEventUpdate()
        {
            if (_lastMinerState == VanillaMinerState.Mining && _currentState == VanillaMinerState.Idle)
            {
                //Miningからidleに切り替わったのでイベントを発火
                InvokeChangeStateEvent();
                _lastMinerState = _currentState;
                return;
            }

            if (_currentState == VanillaMinerState.Idle)
                //Idle中は発火しない
                return;

            //マイニング中 この時は常にイベントを発火
            InvokeChangeStateEvent();
        }

        private void InvokeChangeStateEvent()
        {
            var processingRate = 1 - (float)_remainingMillSecond / _defaultMiningTime;
            OnBlockStateChange?.Invoke(new ChangedBlockState(_currentState.ToStr(), _lastMinerState.ToStr(),
                JsonConvert.SerializeObject(new CommonMachineBlockStateChangeData(_currentPower, RequestEnergy, processingRate))));
        }


        private void InsertConnectInventory()
        {
            for (var i = 0; i < _openableInventoryItemDataStoreService.Items.Count; i++)
            {
                var insertedItem = _connectInventoryService.InsertItem(_openableInventoryItemDataStoreService.Items[i]);
                _openableInventoryItemDataStoreService.SetItem(i, insertedItem);
            }
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(EntityId, slot, itemStack));
        }


        #region Implimantion IOpenableInventory

        public ReadOnlyCollection<IItemStack> Items => _openableInventoryItemDataStoreService.Items;

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _openableInventoryItemDataStoreService.InsertItem(itemStack);
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            return _openableInventoryItemDataStoreService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _openableInventoryItemDataStoreService.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _openableInventoryItemDataStoreService.InsertionCheck(itemStacks);
        }

        public void SetItem(int slot, int itemId, int count)
        {
            _openableInventoryItemDataStoreService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemStack);
        }

        #endregion
    }

    public enum VanillaMinerState
    {
        Idle,
        Mining
    }

    public static class VanillaMinerBlockStateConst
    {
        public const string IdleState = "idle";
        public const string MiningState = "mining";
    }

    public static class ProcessStateExtension
    {
        /// <summary>
        ///     <see cref="ProcessState" />をStringに変換します。
        ///     EnumのToStringを使わない理由はアロケーションによる速度低下をなくすためです。
        /// </summary>
        public static string ToStr(this VanillaMinerState state)
        {
            return state switch
            {
                VanillaMinerState.Idle => VanillaMinerBlockStateConst.IdleState,
                VanillaMinerState.Mining => VanillaMinerBlockStateConst.MiningState,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }
}