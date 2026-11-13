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
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     電力割合とフィルター装填に応じて換気し、通電中はフィルターを定率で消費する清浄機
    ///     Air purifier ventilating by power ratio and loaded filters, consuming filters at a fixed rate while powered
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

        // 装填中のフィルター1個が使われたtick数
        // Ticks the loaded filter has been in use
        private uint _filterWearTicks;

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
            _filterWearTicks = GameUpdater.SecondsToTicks(saveData.FilterWearSeconds);

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

            // 通電していてフィルターがある間だけ寿命を進める。部屋やクラスには依存しない
            // Advance the life only while powered with a filter loaded; independent of rooms and classes
            if (PowerRatio <= 0 || !HasFilterItem) return;
            _filterWearTicks++;

            // 寿命に達したらフィルターを1個消費して数え直す
            // Consume one filter when the life is reached and start counting again
            if (_filterWearTicks < _filterLifeTicks) return;
            _filterWearTicks = 0;
            FilterSlot.SetItem(0, FilterSlot.GetItem(0).SubItem(1));
        }

        public CleanRoomAirFilterStatus GetStatus()
        {
            CheckDestroy(this);

            // 経過tickは装填中の1個に溜まるので、寿命tickとの比から残り寿命を出す
            // Elapsed ticks accrue on the loaded filter, so their ratio to the life ticks gives the remaining life
            var filterCount = HasFilterItem ? FilterSlot.GetItem(0).Count : 0;
            var remainingRatio = filterCount == 0 ? 0d : Math.Max(0d, 1d - (double)_filterWearTicks / _filterLifeTicks);
            return new CleanRoomAirFilterStatus(PowerRatio, RemovalVolumePerSecond, filterCount, remainingRatio);
        }

        public string SaveKey { get; } = typeof(CleanRoomAirFilterComponent).FullName;

        public object GetSaveState()
        {
            CheckDestroy(this);

            var saveData = new CleanRoomAirFilterSaveJsonObject
            {
                FilterWearSeconds = GameUpdater.TicksToSeconds(_filterWearTicks),
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
