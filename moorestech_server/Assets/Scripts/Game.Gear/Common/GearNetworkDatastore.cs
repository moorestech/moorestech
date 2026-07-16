using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Tick;
using Game.Gear.Topology;

namespace Game.Gear.Common
{
    // live gearと適用済み状態を分離する
    // Separates live gear registration from all derived state applied at the tick boundary
    public class GearNetworkDatastore
    {
        private static GearNetworkDatastore _instance;

        private readonly Dictionary<BlockInstanceId, IGearEnergyTransformer> _registeredGears = new();
        private readonly HashSet<IGearOverloadTickTarget> _overloadTickTargets = new();
        private GearNetworkTopologyMap _topologyMap;
        private GearRuntimeStateStore _runtimeStateStore;
        private HashSet<GearNetwork> _networksRequiringRecalc;
        private HashSet<GearNetwork> _continuousTickNetworks;
        private bool _isTopologyDirty = true;

        public bool IsTopologyDirty => _isTopologyDirty;

        public GearNetworkDatastore()
        {
            _instance = this;
            _topologyMap = GearNetworkTopologyMap.CreateEmpty();
            _runtimeStateStore = new GearRuntimeStateStore();
            _networksRequiringRecalc = new HashSet<GearNetwork>();
            _continuousTickNetworks = new HashSet<GearNetwork>();
            GearRuntimeStateStore.Activate(_runtimeStateStore);
        }

        internal GearRuntimeStateStore RuntimeStateStore => _runtimeStateStore;
        internal IReadOnlyCollection<GearNetwork> ContinuousTickNetworks => _continuousTickNetworks;

        public static void AddGear(IGearEnergyTransformer gear)
        {
            _instance._registeredGears[gear.BlockInstanceId] = gear;
            _instance._isTopologyDirty = true;
        }

        public static void RemoveGear(IGearEnergyTransformer gear)
        {
            _instance._registeredGears.Remove(gear.BlockInstanceId);
            _instance._isTopologyDirty = true;
        }

        public static void MarkTopologyDirty()
        {
            _instance._isTopologyDirty = true;
        }

        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;

            // 完成まで旧gear状態を維持する
            // Retain every currently applied object until all replacement state is built and validated
            var rebuilt = GearNetworkTopologyMap.Build(_registeredGears.Values);
            var previousTopologyMap = _topologyMap;
            var previousRuntimeStateStore = _runtimeStateStore;
            var previousRecalcNetworks = _networksRequiringRecalc;
            var previousContinuousNetworks = _continuousTickNetworks;

            // 全参照交換後にruntimeを有効化する
            // Point the static runtime reference at the same new state after exception-free reference swaps
            _topologyMap = rebuilt.TopologyMap;
            _runtimeStateStore = rebuilt.RuntimeStateStore;
            _networksRequiringRecalc = rebuilt.NetworksRequiringRecalc;
            _continuousTickNetworks = rebuilt.ContinuousTickNetworks;
            GearRuntimeStateStore.Activate(_runtimeStateStore);

            previousTopologyMap.Destroy();
            previousRuntimeStateStore.Destroy();
            previousRecalcNetworks.Clear();
            previousContinuousNetworks.Clear();
            _isTopologyDirty = false;
        }

        public static void RegisterOverloadTickTarget(IGearOverloadTickTarget target)
        {
            _instance._overloadTickTargets.Add(target);
        }

        public static void UnregisterOverloadTickTarget(IGearOverloadTickTarget target)
        {
            _instance._overloadTickTargets.Remove(target);
        }

        internal void CollectOverloadTickTargets(List<IGearOverloadTickTarget> buffer)
        {
            buffer.AddRange(_overloadTickTargets);
        }

        public static void NotifyGeneratorOutputChanged(IGearEnergyTransformer generator)
        {
            AddAppliedNetworkToRecalculation(generator);
        }

        public static void NotifyConsumerDemandChanged(IGearEnergyTransformer consumer)
        {
            AddAppliedNetworkToRecalculation(consumer);
        }

        public static bool TryGetGearNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            return _instance._topologyMap.TryGetNetwork(blockInstanceId, out network);
        }

        public static GearNetwork GetGearNetwork(BlockInstanceId blockInstanceId)
        {
            return _instance._topologyMap.GetNetwork(blockInstanceId);
        }

        internal void CollectNetworksRequiringRecalc(List<GearNetwork> buffer)
        {
            buffer.AddRange(_networksRequiringRecalc);
            _networksRequiringRecalc.Clear();
        }

        private static void AddAppliedNetworkToRecalculation(IGearEnergyTransformer gear)
        {
            if (_instance._topologyMap.TryGetNetwork(gear.BlockInstanceId, out var network))
                _instance._networksRequiringRecalc.Add(network);
        }
    }
}
