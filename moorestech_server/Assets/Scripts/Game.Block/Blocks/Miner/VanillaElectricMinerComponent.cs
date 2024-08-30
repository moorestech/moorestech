using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.Util;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using Game.Map.Interface.Vein;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMinerComponent : IElectricConsumer, IBlockInventory, IOpenableInventory, IBlockSaveState, IBlockStateChange, IUpdatableBlockComponent
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly Subject<BlockState> _blockStateChangeSubject = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly List<IItemStack> _miningItems = new();
        
        private readonly OpenableInventoryItemDataStoreService _openableInventoryItemDataStoreService;
        
        private ElectricPower _currentPower;
        private VanillaMinerState _currentState = VanillaMinerState.Idle;
        
        private float _defaultMiningTime = float.MaxValue;
        
        private VanillaMinerState _lastMinerState;
        private double _remainingSecond = double.MaxValue;
        
        public VanillaElectricMinerComponent(BlockId blockId, BlockInstanceId blockInstanceId, ElectricPower requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockConnectorComponent<IBlockInventory> inputConnectorComponent, BlockPositionInfo blockPositionInfo)
        {
            BlockInstanceId = blockInstanceId;
            RequestEnergy = requestPower;
            
            _blockInventoryUpdate = openableInventoryUpdateEvent;
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            _openableInventoryItemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlotCount);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);
            
            SetMiningItem();
            
            #region Internal
            
            void SetMiningItem()
            {
                List<IMapVein> veins = ServerContext.MapVeinDatastore.GetOverVeins(blockPositionInfo.OriginalPos);
                foreach (var vein in veins) _miningItems.Add(itemStackFactory.Create(vein.VeinItemId, 1));
                if (veins.Count == 0) return;
                
                var minerBlockParam = BlockMaster.GetBlockMaster(blockId).BlockParam as ElectricMinerBlockParam;
                foreach (var miningSetting in minerBlockParam.MineSettings)
                {
                    var itemId = ItemMaster.GetItemId(miningSetting.ItemId);
                    if (itemId != veins[0].VeinItemId) continue;
                    _defaultMiningTime = miningSetting.Time;
                    _remainingSecond = _defaultMiningTime;
                    break;
                }
            }
            
            #endregion
        }
        
        public VanillaElectricMinerComponent(string saveData, BlockId blockId, BlockInstanceId blockInstanceId, ElectricPower requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockConnectorComponent<IBlockInventory> inputConnectorComponent, BlockPositionInfo blockPositionInfo)
            : this(blockId, blockInstanceId, requestPower, outputSlotCount, openableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo)
        {
            var saveJsonObject = JsonConvert.DeserializeObject<VanillaElectricMinerSaveJsonObject>(saveData);
            for (var i = 0; i < saveJsonObject.Items.Count; i++)
            {
                var itemStack = saveJsonObject.Items[i].ToItem();
                _openableInventoryItemDataStoreService.SetItem(i, itemStack);
            }
            
            _remainingSecond = saveJsonObject.RemainingSecond;
        }
        
        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.GetItem(slot);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            _openableInventoryItemDataStoreService.SetItem(slot, itemStack);
        }
        
        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _openableInventoryItemDataStoreService.GetSlotSize();
        }
        
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            var saveData = new VanillaElectricMinerSaveJsonObject
            {
                RemainingSecond = _remainingSecond,
                Items = _openableInventoryItemDataStoreService.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
            
            return JsonConvert.SerializeObject(saveData);
        }
        
        public IObservable<BlockState> OnChangeBlockState => _blockStateChangeSubject;
        
        public BlockState GetBlockState()
        {
            var processingRate = 1 - (float)_remainingSecond / _defaultMiningTime;
            var binaryData = MessagePackSerializer.Serialize(new CommonMachineBlockStateChangeData(_currentPower.AsPrimitive(), RequestEnergy.AsPrimitive(), processingRate));
            var state = new BlockState(_currentState.ToStr(), _lastMinerState.ToStr(), binaryData);
            return state;
        }
        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }
        public ElectricPower RequestEnergy { get; }
        
        
        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            
            _currentPower = power;
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            MinerProgressUpdate();
            CheckStateAndInvokeEventUpdate();
            
            #region Internal
            
            void MinerProgressUpdate()
            {
                var subTime = MachineCurrentPowerToSubSecond.GetSubSecond(_currentPower, RequestEnergy);
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
                
                _remainingSecond -= subTime;
                
                if (_remainingSecond <= 0)
                {
                    _remainingSecond = _defaultMiningTime;
                    
                    //空きスロットを探索し、あるならアイテムを挿入
                    _openableInventoryItemDataStoreService.InsertItem(_miningItems);
                }
                
                _currentPower = new ElectricPower(0);
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
                BlockException.CheckDestroy(this);
                
                var state = GetBlockState();
                _blockStateChangeSubject.OnNext(state);
            }
            
            
            void InsertConnectInventory()
            {
                BlockException.CheckDestroy(this);
                
                for (var i = 0; i < _openableInventoryItemDataStoreService.InventoryItems.Count; i++)
                {
                    var insertedItem = _connectInventoryService.InsertItem(_openableInventoryItemDataStoreService.InventoryItems[i]);
                    _openableInventoryItemDataStoreService.SetItem(i, insertedItem);
                }
            }
            
            #endregion
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            _blockInventoryUpdate.OnInventoryUpdateInvoke(
                new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }
        
        #region Implimantion IOpenableInventory
        
        public IReadOnlyList<IItemStack> InventoryItems => _openableInventoryItemDataStoreService.InventoryItems;
        
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemId, count);
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.InsertItem(itemStack);
        }
        
        public IItemStack InsertItem(ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.InsertItem(itemId, count);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.InsertItem(itemStacks);
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.InsertionCheck(itemStacks);
        }
        
        public void SetItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            _openableInventoryItemDataStoreService.SetItem(slot, itemId, count);
        }
        
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.ReplaceItem(slot, itemStack);
        }
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            return _openableInventoryItemDataStoreService.CreateCopiedItems();
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
    
    public class VanillaElectricMinerSaveJsonObject
    {
        [JsonProperty("items")]
        public List<ItemStackSaveJsonObject> Items;
        [JsonProperty("remainingSecond")]
        public double RemainingSecond;
    }
}