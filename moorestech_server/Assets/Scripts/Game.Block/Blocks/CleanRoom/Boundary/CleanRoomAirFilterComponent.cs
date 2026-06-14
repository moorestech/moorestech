using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // エアフィルター本体（単一コンポーネント初版）。電力割合で実効q、除去量に比例してフィルター摩耗。
    // Air filter core (single-component v1): effective q scales with power; filter wears by removed amount.
    public class CleanRoomAirFilterComponent : IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter
    {
        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }
        public string SaveKey => "cleanRoomAirFilter";

        // セーブ/テスト検証用の摩耗累計（filterCapacity 未満の端数）。
        // Wear accumulator below one filterCapacity; exposed for save/tests.
        public double WearProgress => _wearProgress;

        private readonly double _removalVolumePerSecond; // 満電1台の q
        private readonly float _requiredPower;
        private readonly double _filterCapacity;
        private readonly CleanRoomAirFilterItemComponent _filterInventory;

        // 常時消費のため毎Updateで電力を使う（Vanilla機械の「Processing中のみ消費」とは意図的に異なる）。
        // Always-on consumer: power is spent every Update (deliberately unlike Vanilla's processing-only spend).
        private bool _usedPower;
        private float _currentPower;
        private double _wearProgress;

        public CleanRoomAirFilterComponent(BlockInstanceId blockInstanceId, double removalVolumePerSecond, float requiredPower, double filterCapacity, CleanRoomAirFilterItemComponent filterInventory)
        {
            BlockInstanceId = blockInstanceId;
            _removalVolumePerSecond = removalVolumePerSecond;
            _requiredPower = requiredPower;
            _filterCapacity = filterCapacity;
            _filterInventory = filterInventory;
        }

        public CleanRoomAirFilterComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, double removalVolumePerSecond, float requiredPower, double filterCapacity, CleanRoomAirFilterItemComponent filterInventory)
            : this(blockInstanceId, removalVolumePerSecond, requiredPower, filterCapacity, filterInventory)
        {
            if (!componentStates.TryGetValue(SaveKey, out var stateRaw)) return;
            var json = JsonConvert.DeserializeObject<CleanRoomAirFilterSaveJsonObject>(stateRaw);
            _wearProgress = json.WearProgress;
        }

        #region IElectricConsumer

        public ElectricPower RequestEnergy => new ElectricPower(_requiredPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _usedPower = false;
            _currentPower = power.AsPrimitive();
        }

        #endregion

        // q × 電力割合(≤1) × (フィルター残>0 ? 1 : 0)。
        // q × power-ratio(≤1) × (filter present ? 1 : 0).
        public double RemovalVolumePerSecond
        {
            get
            {
                if (!_filterInventory.HasFilter) return 0.0;
                if (_requiredPower <= 0f) return _removalVolumePerSecond;
                var ratio = _currentPower / _requiredPower;
                if (ratio > 1f) ratio = 1f;
                if (ratio < 0f) ratio = 0f;
                return _removalVolumePerSecond * ratio;
            }
        }

        // データストアが今tickの除去量を渡す。累計が filterCapacity を跨ぐごとに1個消費。
        // Datastore pushes this tick's removed amount; consume one filter per capacity crossed.
        public void ApplyRemovedImpurity(double removed)
        {
            BlockException.CheckDestroy(this);
            if (removed <= 0) return;
            _wearProgress += removed;
            while (_wearProgress >= _filterCapacity && _filterInventory.TryConsumeOneFilter())
            {
                _wearProgress -= _filterCapacity;
            }
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_usedPower)
            {
                _usedPower = false;
                _currentPower = 0f;
            }
            _usedPower = true;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return JsonConvert.SerializeObject(new CleanRoomAirFilterSaveJsonObject { WearProgress = _wearProgress });
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }

    public class CleanRoomAirFilterSaveJsonObject
    {
        [JsonProperty("wearProgress")] public double WearProgress;
    }
}
