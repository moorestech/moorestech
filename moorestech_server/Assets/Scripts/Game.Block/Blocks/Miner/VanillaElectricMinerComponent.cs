using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.Util;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMinerComponent : IElectricConsumer, IBlockInventory, IOpenableInventory, IBlockSaveState, IBlockStateChange
    {
        public bool IsDestroy { get; private set; }

        public IObservable<ChangedBlockState> BlockStateChange => _blockStateChangeSubject;
        private readonly Subject<ChangedBlockState> _blockStateChangeSubject = new();

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;

        private readonly OpenableInventoryItemDataStoreService _openableInventoryItemDataStoreService;

        private int _currentPower;
        private VanillaMinerState _currentState = VanillaMinerState.Idle;

        private int _defaultMiningTime = int.MaxValue;

        private VanillaMinerState _lastMinerState;
        private List<IItemStack> _miningItems = new();
        private int _remainingMillSecond = int.MaxValue;

        private readonly IDisposable _updateObservable;

        public VanillaElectricMinerComponent(int blockId, int entityId, int requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockConnectorComponent<IBlockInventory> inputConnectorComponent, BlockPositionInfo blockPositionInfo)
        {
            EntityId = entityId;
            RequestEnergy = requestPower;

            _blockInventoryUpdate = openableInventoryUpdateEvent;

            var itemStackFactory = ServerContext.ItemStackFactory;
            _openableInventoryItemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlotCount);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);

            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());

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

        public VanillaElectricMinerComponent(string saveData, int blockId, int entityId, int requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockConnectorComponent<IBlockInventory> inputConnectorComponent, BlockPositionInfo blockPositionInfo)
            : this(blockId, entityId, requestPower, outputSlotCount, openableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo)
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


        public int EntityId { get; }

        public string GetSaveState()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            //_remainingMillSecond,itemId1,itemCount1,itemId2,itemCount2,itemId3,itemCount3...
            var saveState = $"{_remainingMillSecond}";
            foreach (var itemStack in _openableInventoryItemDataStoreService.Items)
                saveState += $",{itemStack.ItemHash},{itemStack.Count}";

            return saveState;
        }

        public IItemStack GetItem(int slot)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            _openableInventoryItemDataStoreService.SetItem(slot, itemStack);
        }

        public int GetSlotSize()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.GetSlotSize();
        }

        public int RequestEnergy { get; }


        public void SupplyEnergy(int power)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            _currentPower = power;
        }

        private void Update()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            MinerProgressUpdate();
            CheckStateAndInvokeEventUpdate();

            #region Internal

            void MinerProgressUpdate()
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

            void CheckStateAndInvokeEventUpdate()
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

            void InvokeChangeStateEvent()
            {
                if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

                var processingRate = 1 - (float)_remainingMillSecond / _defaultMiningTime;
                var binaryData = MessagePackSerializer.Serialize(new CommonMachineBlockStateChangeData(_currentPower, RequestEnergy, processingRate));
                var changeStateData = new ChangedBlockState(_currentState.ToStr(), _lastMinerState.ToStr(), binaryData);
                _blockStateChangeSubject.OnNext(changeStateData);
            }


            void InsertConnectInventory()
            {
                if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

                for (var i = 0; i < _openableInventoryItemDataStoreService.Items.Count; i++)
                {
                    var insertedItem = _connectInventoryService.InsertItem(_openableInventoryItemDataStoreService.Items[i]);
                    _openableInventoryItemDataStoreService.SetItem(i, insertedItem);
                }
            }

  #endregion
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            _blockInventoryUpdate.OnInventoryUpdateInvoke(
                new BlockOpenableInventoryUpdateEventProperties(EntityId, slot, itemStack));
        }

        #region Implimantion IOpenableInventory

        public ReadOnlyCollection<IItemStack> Items => _openableInventoryItemDataStoreService.Items;

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.InsertItem(itemStack);
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.InsertionCheck(itemStacks);
        }

        public void SetItem(int slot, int itemId, int count)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            _openableInventoryItemDataStoreService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemStack);
        }

        #endregion

        public void Destroy()
        {
            IsDestroy = true;
            _updateObservable.Dispose();
        }
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