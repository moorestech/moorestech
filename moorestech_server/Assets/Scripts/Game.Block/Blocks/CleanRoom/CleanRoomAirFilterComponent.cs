using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
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
    ///     電力割合とフィルター装填に応じて部屋の不純物を除去する清浄機
    ///     Air purifier removing room impurities based on power ratio and loaded filters
    /// </summary>
    public class CleanRoomAirFilterComponent : IElectricConsumer, IElectricTickPostHandler, IBlockSaveState, ICleanRoomAirFilter
    {
        // 実効除去体積 = 基本値 × 電力割合 × フィルター有無
        // Effective removal volume = base x power ratio x filter presence
        public double RemovalVolumePerSecond => _removalVolumePerSecond * PowerRatio * (HasFilterItem ? 1 : 0);

        public readonly OpenableInventoryItemDataStoreService FilterSlot;

        private readonly float _requiredPower;
        private readonly double _removalVolumePerSecond;
        private readonly double _filterCapacity;
        private readonly ItemId _filterItemId;

        // 電力tickの後処理で確定した現在電力
        // The current power settled by the electric tick post-process
        private float _currentPower;
        private double _wearAccumulation;

        public CleanRoomAirFilterComponent(BlockInstanceId blockInstanceId, CleanRoomAirFilterBlockParam param)
        {
            BlockInstanceId = blockInstanceId;
            _requiredPower = param.RequiredPower;
            _removalVolumePerSecond = param.RemovalVolumePerSecond;
            _filterCapacity = param.FilterCapacity;
            _filterItemId = MasterHolder.ItemMaster.GetItemId(param.FilterItemGuid);
            FilterSlot = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, 1);
        }

        public CleanRoomAirFilterComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, CleanRoomAirFilterBlockParam param) : this(blockInstanceId, param)
        {
            if (!componentStates.TryGetValue(SaveKey, out var stateRaw)) return;

            var saveData = JsonConvert.DeserializeObject<CleanRoomAirFilterSaveJsonObject>(stateRaw);
            _wearAccumulation = saveData.WearAccumulation;

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

        public void ApplyRemovedImpurity(double removed)
        {
            CheckDestroy(this);
            _wearAccumulation += removed;

            // 摩耗累計が容量に達するごとにフィルターを1個消費する
            // Consume one filter each time the accumulated wear reaches the capacity
            while (_wearAccumulation >= _filterCapacity && HasFilterItem)
            {
                _wearAccumulation -= _filterCapacity;
                FilterSlot.SetItem(0, FilterSlot.GetItem(0).SubItem(1));
            }
        }

        public string SaveKey { get; } = typeof(CleanRoomAirFilterComponent).FullName;

        public string GetSaveState()
        {
            CheckDestroy(this);

            var saveData = new CleanRoomAirFilterSaveJsonObject
            {
                WearAccumulation = _wearAccumulation,
                Items = FilterSlot.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
            return JsonConvert.SerializeObject(saveData);
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
        [JsonProperty("wearAccumulation")]
        public double WearAccumulation;

        [JsonProperty("inventory")]
        public List<ItemStackSaveJsonObject> Items;
    }
}
