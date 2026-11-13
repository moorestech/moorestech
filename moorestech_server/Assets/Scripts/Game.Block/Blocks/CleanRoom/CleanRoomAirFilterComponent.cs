using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     電力割合とフィルター装填に応じて換気し、成立した部屋の中でだけ電力割合に比例してフィルターを消費する清浄機
    ///     Air purifier ventilating by power ratio and loaded filters, consuming filters in proportion to power only while serving a detected room
    /// </summary>
    public class CleanRoomAirFilterComponent : IElectricConsumer, IElectricTickPostHandler, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter
    {
        // 実効除去体積 = 基本値 × 電力割合 × フィルター有無
        // Effective removal volume = base x power ratio x filter presence
        public double RemovalVolumePerSecond => _removalVolumePerSecond * PowerRatio * (HasFilterItem ? 1 : 0);

        public readonly OpenableInventoryItemDataStoreService FilterSlot;

        private readonly float _requiredPower;
        private readonly double _removalVolumePerSecond;
        private readonly uint _filterLifeTicks;
        private readonly ItemId _filterItemId;

        // 電力tickの後処理で確定した現在電力
        // The current power settled by the electric tick post-process
        private float _currentPower;

        // 装填中のフィルター1個が使われたtick数。電力割合ぶんずつ進むので端数を持つ
        // Ticks the loaded filter has been in use; it advances by the power ratio, so it keeps fractions
        private double _filterWearTicks;

        // 成立した部屋に属しているか。クリーンルーム判定段が変化時にプッシュし、ロード直後は最初の判定tickまでfalse
        // Whether the purifier serves a detected room; the clean-room pass pushes changes, false until its first pass after load
        private bool _isServingRoom;

        public CleanRoomAirFilterComponent(BlockInstanceId blockInstanceId, CleanRoomAirFilterBlockParam param)
        {
            BlockInstanceId = blockInstanceId;
            _requiredPower = param.RequiredPower;
            _removalVolumePerSecond = param.RemovalVolumePerSecond;

            // 寿命が正であることはマスタ検証で保証済み
            // The master validation guarantees a positive life
            _filterLifeTicks = GameUpdater.SecondsToTicks(param.FilterLifeSeconds);
            _filterItemId = MasterHolder.ItemMaster.GetItemId(param.FilterItemGuid);
            FilterSlot = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, 1);
        }

        public CleanRoomAirFilterComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId, CleanRoomAirFilterBlockParam param) : this(blockInstanceId, param)
        {
            if (!BlockComponentStateReader.TryRead<CleanRoomAirFilterSaveJsonObject>(componentStates, SaveKey, out var saveData)) return;
            _filterWearTicks = ReadSavedWearTicks(saveData.FilterWearSeconds);

            // ロード時はブロック未登録のためイベント無しでスロットを復元する
            // Restore the slot without events since the block is not yet registered on load
            if (saveData.Items == null) return;
            for (var i = 0; i < saveData.Items.Count && i < FilterSlot.GetSlotSize(); i++)
                FilterSlot.SetItemWithoutEvent(i, saveData.Items[i].ToItemStack());
        }

        public BlockInstanceId BlockInstanceId { get; }

        public ElectricPower RequestEnergy => new(_requiredPower);

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            CheckDestroy(this);

            // 電力tickで確定した供給率を現在電力へ反映する
            // Apply the electric tick's settled supply rate to the current power
            _currentPower = _requiredPower * statistics.PowerRate;
        }

        public void Update()
        {
            CheckDestroy(this);

            // 部屋の中で通電しフィルターがある間だけ、電力割合ぶん寿命を進める。クラスには依存しない
            // Advance the life by the power ratio only while serving a room, powered and loaded; independent of the class
            if (!_isServingRoom || PowerRatio <= 0 || !HasFilterItem) return;
            _filterWearTicks += PowerRatio;

            // 寿命に達したらフィルターを1個消費し、超過分の端数は次の1個へ持ち越す
            // Consume one filter when the life is reached and carry the overshoot into the next one
            if (_filterWearTicks < _filterLifeTicks) return;
            _filterWearTicks -= _filterLifeTicks;
            FilterSlot.SetItem(0, FilterSlot.GetItem(0).SubItem(1));
        }

        public void SetServingRoom(bool isServingRoom)
        {
            CheckDestroy(this);
            _isServingRoom = isServingRoom;
        }

        public CleanRoomAirFilterStatus GetStatus()
        {
            CheckDestroy(this);

            // 経過tickは装填中の1個に溜まるので、寿命tickとの比から残り寿命を出す
            // Elapsed ticks accrue on the loaded filter, so their ratio to the life ticks gives the remaining life
            var filterCount = HasFilterItem ? FilterSlot.GetItem(0).Count : 0;
            var remainingRatio = filterCount == 0 ? 0d : Math.Max(0d, 1d - _filterWearTicks / _filterLifeTicks);
            return new CleanRoomAirFilterStatus(PowerRatio, RemovalVolumePerSecond, filterCount, remainingRatio);
        }

        public string SaveKey { get; } = typeof(CleanRoomAirFilterComponent).FullName;

        public object GetSaveState()
        {
            CheckDestroy(this);

            var saveData = new CleanRoomAirFilterSaveJsonObject
            {
                FilterWearSeconds = _filterWearTicks * GameUpdater.SecondsPerTick,
                Items = FilterSlot.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
            return saveData;
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        // 要求電力0の定義ミスでも0除算せず全力扱いにする
        // Treat a zero required power as full ratio instead of dividing by zero
        private double PowerRatio => _requiredPower <= 0 ? 1d : Math.Min(1d, _currentPower / (double)_requiredPower);

        private bool HasFilterItem
        {
            get
            {
                var stack = FilterSlot.GetItem(0);
                return stack.Id == _filterItemId && stack.Count > 0;
            }
        }

        // 端数を保ったまま秒をtickへ戻す。負・非数・無限大の壊れたセーブ値は未使用として読み、ログに残す
        // Convert seconds back to ticks keeping fractions; a broken negative, NaN or infinite value is read as unused and logged
        private double ReadSavedWearTicks(double filterWearSeconds)
        {
            var wearTicks = filterWearSeconds * GameUpdater.TicksPerSecond;
            if (0 <= wearTicks && !double.IsInfinity(wearTicks)) return wearTicks;

            Debug.LogError($"[CleanRoomAirFilter] BlockInstanceId:{BlockInstanceId} has invalid filterWearSeconds:{filterWearSeconds}; treating the loaded filter as unused");
            return 0d;
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }
    }

    public class CleanRoomAirFilterSaveJsonObject
    {
        [JsonProperty("filterWearSeconds")]
        public double FilterWearSeconds;

        [JsonProperty("inventory")]
        public List<ItemStackSaveJsonObject> Items;
    }
}
