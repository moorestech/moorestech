using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Util;
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

        // 装填中のフィルター1個が使われたtick数。電力割合を確率で丸めた整数tickずつ進む
        // Ticks the loaded filter has been in use; advances by whole ticks, the power ratio rounded probabilistically
        private uint _filterWearTicks;

        // 成立した部屋に属しているか。クリーンルーム判定段が変化時にプッシュし、ロード時はセーブから戻す
        // Whether the purifier serves a detected room; the clean-room pass pushes changes, and on load it is restored from the save
        public bool IsServingRoom { get; private set; }

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

            // 保存tickの判定段が最後に配った在室を戻し、ロード直後のtickを稼働中の世界と同じ在室で回す
            // Restore the serving state the save tick's pass last pushed so the first tick after load runs as the live world would
            IsServingRoom = saveData.IsServingRoom;

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

            // 部屋の中で通電しフィルターがある間だけ、採掘機と同じく電力割合を確率で丸めたtick数ぶん寿命を進める
            // Only while serving a room, powered and loaded, advance the life by power-scaled sub-ticks as the miner does
            if (!IsServingRoom || PowerRatio <= 0 || !HasFilterItem) return;
            _filterWearTicks += MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, _requiredPower);

            // 寿命に達したらフィルターを1個消費し、採掘機の周期と同じく次の1個は0から数える
            // Consume one filter on reaching the life; like the miner's cycle, the next one counts from zero
            if (_filterWearTicks < _filterLifeTicks) return;
            _filterWearTicks = 0;
            FilterSlot.SetItem(0, FilterSlot.GetItem(0).SubItem(1));
        }

        public void SetServingRoom(bool isServingRoom)
        {
            CheckDestroy(this);
            IsServingRoom = isServingRoom;
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
                IsServingRoom = IsServingRoom,
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

        // 秒をtickへ戻し、寿命の変更で超えた値は採掘機と同じく寿命で頭打ちにする。負・非数・無限大の壊れたセーブ値は未使用として読み、ログに残す
        // Convert seconds back to ticks, capping at the life like the miner when the life shrank; a broken negative, NaN or infinite value is read as unused and logged
        private uint ReadSavedWearTicks(double filterWearSeconds)
        {
            if (0 <= filterWearSeconds && !double.IsInfinity(filterWearSeconds)) return Math.Min(GameUpdater.SecondsToTicks(filterWearSeconds), _filterLifeTicks);

            Debug.LogError($"[CleanRoomAirFilter] BlockInstanceId:{BlockInstanceId} has invalid filterWearSeconds:{filterWearSeconds}; treating the loaded filter as unused");
            return 0;
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

        // 欠けたセーブを在室なしで黙って読むと稼働中の世界と寿命がずれるので、キー欠落はロードで止める
        // A missing key would silently read as not serving and drift from the live world, so loading fails on it
        [JsonProperty("isServingRoom", Required = Required.Always)]
        public bool IsServingRoom;

        [JsonProperty("inventory")]
        public List<ItemStackSaveJsonObject> Items;
    }
}
