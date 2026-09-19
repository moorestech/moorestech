using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.Util;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Vein;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Interface.State;
using Game.Context;
using Game.Map.Interface.Vein;
using MessagePack;
using Mooresmaster.Model.MineSettingsModule;
using UniRx;
using UnityEngine;
using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Block.Blocks.Miner
{
    public class VanillaMinerProcessorComponent : IOpenableBlockInventoryComponent, IBlockSaveState, IBlockStateObservable, IUpdatableBlockComponent
    {
        public bool IsDestroy { get; private set; }
        public float RequestEnergy => _baseRequestEnergy * (_currentState == VanillaMinerState.Mining ? 1f : _idlePowerRate);
        public bool IsMining => _currentState == VanillaMinerState.Mining;
        public IObservable<Unit> OnChangeBlockState => _blockStateChangeSubject;
        private Subject<Unit> _blockStateChangeSubject = new();
        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly List<IItemStack> _miningItems = new();
        
        private readonly OpenableInventoryItemDataStoreService _openableInventoryItemDataStoreService;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly float _baseRequestEnergy;
        private readonly float _idlePowerRate;
        
        // 前回のUpdate以降に供給が届いたか。届かなかったtickは給電断として分子を0へ落とす
        // Whether a supply arrived since the previous Update; a tick without one counts as lost supply and zeroes the numerator
        private bool _suppliedSinceLastUpdate;
        private float _currentPower;

        // 分子_currentPowerと同位置・同じ状態基準で確定する配信用の要求電力（前例 MachineProcessContext.PublishedRequestPower）
        // Request power published with the numerator _currentPower, latched at the same point and state basis (precedent: MachineProcessContext.PublishedRequestPower)
        private float _publishedRequestPower;

        // 前回発火時に配信した供給電力。給電の変化を発火条件にする（前例 ElectricPumpProcessorComponent の powerMoved）
        // Supply published at the last fire; a change in it triggers a fire (precedent: ElectricPumpProcessorComponent's powerMoved)
        private float _lastPublishedPower;

        private uint _defaultMiningTicks;
        private uint _remainingTicks;
        
        private VanillaMinerState _lastMinerState;
        private VanillaMinerState _currentState = VanillaMinerState.Idle;
        
        public VanillaMinerProcessorComponent(BlockInstanceId blockInstanceId, float requestPower, float idlePowerRate, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> inputConnectorComponent, BlockPositionInfo blockPositionInfo, MineSettings mineSettings)
        {
            _blockInstanceId = blockInstanceId;
            _baseRequestEnergy = requestPower;
            _idlePowerRate = idlePowerRate;
            
            _blockInventoryUpdate = openableInventoryUpdateEvent;
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            _openableInventoryItemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlotCount);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, inputConnectorComponent);
            
            SetMiningItem();

            // 設置直後の1tick目から正しい要求電力を配信できるよう初期状態でラッチする（前例 ElectricPumpProcessorComponent）
            // Latch the initial request power so it is correct from the first tick even before an Update (precedent: ElectricPumpProcessorComponent)
            _publishedRequestPower = RequestEnergy;

            #region Internal

            void SetMiningItem()
            {
                // 掘れる鉱脈かの合成規則はクライアントの設置判定と同じ1本。同一アイテムは1種1個にまとめる
                // The same composed rule the client placement check uses decides a minable vein; each item appears once
                var minableItemIds = MinerVeinFootprintJudge.ResolveMinableItemIds(mineSettings);
                var targetItemIds = new HashSet<ItemId>();
                foreach (var vein in ServerContext.ItemMapVeinDatastore.Veins)
                {
                    if (!MinerVeinFootprintJudge.IsMinableVein(blockPositionInfo, minableItemIds, vein.VeinRangeMin, vein.VeinRangeMax, vein.VeinItemId)) continue;
                    if (targetItemIds.Add(vein.VeinItemId)) _miningItems.Add(itemStackFactory.Create(vein.VeinItemId, 1));
                }

                // 採掘時間は一致した鉱脈の中で最も遅い値。順序非依存にしてマスタの並びで変わらないようにする
                // The mining time is the slowest among matched veins, independent of master ordering
                foreach (var miningSetting in mineSettings.items)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(miningSetting.ItemGuid);
                    if (!targetItemIds.Contains(itemId)) continue;
                    _defaultMiningTicks = Math.Max(_defaultMiningTicks, GameUpdater.SecondsToTicks(miningSetting.Time));
                }
                _remainingTicks = _defaultMiningTicks;
            }

            #endregion
        }
        
        public VanillaMinerProcessorComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId, float requestPower, float idlePowerRate, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> inputConnectorComponent, BlockPositionInfo blockPositionInfo, MineSettings mineSettings)
            : this(blockInstanceId, requestPower, idlePowerRate, outputSlotCount, openableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo, mineSettings)
        {
            var saveJsonObject = BlockComponentStateReader.Read<VanillaElectricMinerSaveJsonObject>(componentStates, SaveKey);

            // セーブデータからのロード時はイベントを発火しない（ブロックがまだWorldBlockDatastoreに登録されていないため）
            // Do not invoke events when loading from save data (block is not yet registered in WorldBlockDatastore)
            for (var i = 0; i < saveJsonObject.Items.Count; i++)
            {
                var itemStack = saveJsonObject.Items[i].ToItemStack();
                _openableInventoryItemDataStoreService.SetItemWithoutEvent(i, itemStack);
            }

            // 採掘対象が変わったセーブは旧タイマーを引き継がない。1サイクルの長さが変わっており進捗率が範囲外になる
            // A save whose mining targets changed must not inherit the old timer; the cycle length differs and the progress rate would fall outside its range
            if (!IsSameMiningTarget(saveJsonObject.MiningItemGuids)) return;

            // 秒数からtickに変換して復元
            // Convert seconds back to ticks for restoration
            _remainingTicks = Math.Min(GameUpdater.SecondsToTicks(saveJsonObject.RemainingSeconds), _defaultMiningTicks);
        }

        // セーブ時の採掘対象と今の対象が一致するか。旧セーブはGUIDを持たないため不一致として扱う
        // Whether the saved mining targets match the current ones; an older save carries no GUIDs and counts as a mismatch
        private bool IsSameMiningTarget(List<string> savedMiningItemGuids)
        {
            if (savedMiningItemGuids == null || savedMiningItemGuids.Count != _miningItems.Count) return false;

            var savedGuids = new HashSet<string>(savedMiningItemGuids);
            foreach (var miningItem in _miningItems)
                if (!savedGuids.Contains(MasterHolder.ItemMaster.GetItemMaster(miningItem.Id).ItemGuid.ToString()))
                    return false;

            return true;
        }
        
        // tick内限定の内部経路。電気機械は供給率導出値を、歯車機械はRPM・トルク由来の電力相当値を渡す
        // Tick-scoped internal path; electric machines pass the rate-derived power, gear machines pass the RPM/torque-equivalent power
        public void SupplyExternalPower(float power)
        {
            BlockException.CheckDestroy(this);

            // 供給はこの時点のRequestEnergyに対して行われるので、分母も同じ基準で確定する
            // The supply answers the RequestEnergy of this moment, so the denominator is latched on the same basis
            _suppliedSinceLastUpdate = true;
            _currentPower = power;
            _publishedRequestPower = RequestEnergy;
        }
        
        public string SaveKey { get; } = typeof(VanillaMinerProcessorComponent).FullName;
        public object GetSaveState()
        {
            BlockException.CheckDestroy(this);

            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            var saveData = new VanillaElectricMinerSaveJsonObject
            {
                RemainingSeconds = GameUpdater.TicksToSeconds(_remainingTicks),
                MiningItemGuids = _miningItems.Select(item => MasterHolder.ItemMaster.GetItemMaster(item.Id).ItemGuid.ToString()).ToList(),
                Items = _openableInventoryItemDataStoreService.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };

            return saveData;
        }
        
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            // 供給が来なかったtickは分子0。分母も状態遷移前の基準で取り直し、古い供給の基準を残さない
            // A tick without supply publishes zero; the denominator is re-latched on the pre-transition basis too
            if (!_suppliedSinceLastUpdate)
            {
                _currentPower = 0f;
                _publishedRequestPower = RequestEnergy;
            }
            _suppliedSinceLastUpdate = false;
            
            MinerProgressUpdate();
            InsertConnectInventory();
            CheckStateAndInvokeEventUpdate();
            
            #region Internal

            void MinerProgressUpdate()
            {
                // 採掘可能性を先に判定し、進行計算はフル要求電力を基準にする
                // Check mining feasibility first, then calculate progress against full demand
                if (_miningItems.Count == 0)
                {
                    _currentState = VanillaMinerState.Idle;
                    return;
                }

                // insertできるかチェック
                // Check if insertion is possible
                if (!_openableInventoryItemDataStoreService.InsertionCheck(_miningItems))
                {
                    // 挿入できないのでreturn
                    // Cannot insert, return
                    _currentState = VanillaMinerState.Idle;
                    return;
                }

                _currentState = VanillaMinerState.Mining;
                var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, _baseRequestEnergy);
                if (subTicks == 0)
                {
                    // 電力の都合で処理を進められないのでreturn
                    // Cannot proceed due to power constraints
                    return;
                }

                if (subTicks >= _remainingTicks)
                {
                    _remainingTicks = _defaultMiningTicks;

                    // 空きスロットを探索し、あるならアイテムを挿入
                    // Find empty slot and insert item if available
                    _openableInventoryItemDataStoreService.InsertItem(_miningItems);
                }
                else
                {
                    _remainingTicks -= subTicks;
                }
            }
            
            // 採掘中は毎tick、待機へ落ちたtickと給電断で配信値が動いたtickに発火し、発火後に前tick状態を更新する
            // Fire every tick while mining, and on the drop to idle or a supply loss that moved the published power; then record this tick's state as the previous one
            void CheckStateAndInvokeEventUpdate()
            {
                var droppedToIdle = _lastMinerState == VanillaMinerState.Mining && _currentState == VanillaMinerState.Idle;
                var powerMoved = !Mathf.Approximately(_lastPublishedPower, _currentPower);
                if (_currentState == VanillaMinerState.Mining || droppedToIdle || powerMoved) InvokeChangeStateEvent();
                _lastMinerState = _currentState;
                _lastPublishedPower = _currentPower;
            }
            
            void InvokeChangeStateEvent()
            {
                BlockException.CheckDestroy(this);
                
                _blockStateChangeSubject.OnNext(Unit.Default);
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
        
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            
            return new []
            {
                GetMachineBlockStateDetail(),
                GetMinerBlockStateDetail(),
            };
            
            #region Internal

            BlockStateDetail GetMachineBlockStateDetail()
            {
                var processingRate = _defaultMiningTicks > 0 ? 1 - (float)_remainingTicks / _defaultMiningTicks : 0;
                var stateDetail = new CommonMachineBlockStateDetail(_currentPower, _publishedRequestPower, processingRate, _currentState.ToStr(), _lastMinerState.ToStr());
                var stateDetailBytes = MessagePackSerializer.Serialize(stateDetail);
                return new BlockStateDetail(CommonMachineBlockStateDetail.BlockStateDetailKey, stateDetailBytes);
            }
            
            BlockStateDetail GetMinerBlockStateDetail()
            {
                var stateDetail = new CommonMinerBlockStateDetail(_miningItems, GameUpdater.TicksToSeconds(_defaultMiningTicks));
                var stateDetailBytes = MessagePackSerializer.Serialize(stateDetail);
                return new BlockStateDetail(CommonMinerBlockStateDetail.BlockStateDetailKey, stateDetailBytes);
            }
            
  #endregion
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(_blockInstanceId, slot, itemStack));
        }
        
        #region Implimantion IOpenableInventory
        
        
        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            
            return _openableInventoryItemDataStoreService.GetItem(slot);
        }
        
        // 配置制約を持たないインベントリはどのスロットも受け入れる
        // An inventory without placement restrictions accepts every slot
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            return true;
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

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            return InsertItem(itemStack);
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
        
        public void Destroy()
        {
            IsDestroy = true;
            _blockStateChangeSubject.Dispose();
            _blockStateChangeSubject = null;
        }
    }
}
