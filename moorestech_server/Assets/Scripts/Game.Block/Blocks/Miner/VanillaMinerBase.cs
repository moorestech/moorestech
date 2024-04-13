using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.EnergySystem;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.Util;
using Game.Block.Component;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using Game.Context;
using Game.Map.Interface.Vein;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Miner
{
    public abstract class VanillaMinerBase : IBlock, IEnergyConsumer, IBlockInventory, IMiner, IOpenableInventory
    {
        private readonly BlockComponentManager _blockComponentManager = new();

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly Subject<ChangedBlockState> _blockStateChangeSubject = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;

        private readonly OpenableInventoryItemDataStoreService _openableInventoryItemDataStoreService;

        private int _currentPower;
        private VanillaMinerState _currentState = VanillaMinerState.Idle;

        private int _defaultMiningTime = int.MaxValue;

        private VanillaMinerState _lastMinerState;
        private List<IItemStack> _miningItems = new();
        private int _remainingMillSecond = int.MaxValue;

        protected VanillaMinerBase(int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockPositionInfo blockPositionInfo)
        {
            BlockId = blockId;
            EntityId = entityId;
            RequestEnergy = requestPower;
            BlockHash = blockHash;

            _blockInventoryUpdate = openableInventoryUpdateEvent;
            BlockPositionInfo = blockPositionInfo;

            var inputConnectorComponent = new InventoryInputConnectorComponent(
                new IOConnectionSetting(
                    new ConnectDirection[] { },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor }), blockPositionInfo);
            _blockComponentManager.AddComponent(inputConnectorComponent);

            var itemStackFactory = ServerContext.ItemStackFactory;
            _openableInventoryItemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlotCount);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);

            GameUpdater.UpdateObservable.Subscribe(_ => Update());
            
            SetMiningItem();

            #region Internal

            void SetMiningItem()
            {
                var veins = ServerContext.MapVeinDatastore.GetOverVeins(blockPositionInfo.OriginalPos);
                foreach (var vein in veins)
                {
                    _miningItems.Add(itemStackFactory.Create(vein.VeinItemId, 1));
                }
                if (veins.Count == 0) return;
                
                var blockConfig = ServerContext.BlockConfig.GetBlockConfig(blockId).Param as MinerBlockConfigParam;
                foreach (var miningSetting in blockConfig.MineItemSettings)
                {
                    if (miningSetting.ItemId != veins[0].VeinItemId) continue;
                    _defaultMiningTime = miningSetting.MiningTime;
                    _remainingMillSecond = _defaultMiningTime;
                    break;
                }
            }
            
            #endregion
        }

        protected VanillaMinerBase(string saveData, int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount,
            BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockPositionInfo blockPositionInfo)
            : this(blockId, entityId, blockHash, requestPower, outputSlotCount, openableInventoryUpdateEvent, blockPositionInfo)
        {
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var split = saveData.Split(',');
            _remainingMillSecond = int.Parse(split[0]);
            var inventoryItems = new List<IItemStack>();
            for (var i = 1; i < split.Length; i += 2)
            {
                var itemHash = long.Parse(split[i]);
                var itemCount = int.Parse(split[i + 1]);
                var item = ServerContext.ItemStackFactory.Create(itemHash, itemCount);
                inventoryItems.Add(item);
            }

            for (var i = 0; i < inventoryItems.Count; i++)
                _openableInventoryItemDataStoreService.SetItem(i, inventoryItems[i]);
        }
        public IBlockComponentManager ComponentManager => _blockComponentManager;

        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<ChangedBlockState> BlockStateChange => _blockStateChangeSubject;

        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }

        public string GetSaveState()
        {
            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = $"{_remainingMillSecond}";
            foreach (var itemStack in _openableInventoryItemDataStoreService.Items)
                saveState += $",{itemStack.ItemHash},{itemStack.Count}";

            return saveState;
        }

        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return EntityId == other.EntityId && BlockId == other.BlockId && BlockHash == other.BlockHash;
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

        private void Update()
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
            var jsonData = JsonConvert.SerializeObject(new CommonMachineBlockStateChangeData(_currentPower, RequestEnergy, processingRate));
            var changeStateData = new ChangedBlockState(_currentState.ToStr(), _lastMinerState.ToStr(), jsonData);
            _blockStateChangeSubject.OnNext(changeStateData);
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
            _blockInventoryUpdate.OnInventoryUpdateInvoke(
                new BlockOpenableInventoryUpdateEventProperties(EntityId, slot, itemStack));
        }

        public override bool Equals(object obj)
        {
            return obj is IBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EntityId, BlockId, BlockHash);
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
        Mining,
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
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}