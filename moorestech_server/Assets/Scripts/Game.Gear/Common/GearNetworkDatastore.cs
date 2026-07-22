using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Tick;
using Game.Gear.Topology;

namespace Game.Gear.Common
{
    // live gearと適用済み状態を分離する
    // Separates live gear registration from all derived state applied at the tick boundary
    public class GearNetworkDatastore : IGearNetworkDatastore
    {
        private readonly Dictionary<BlockInstanceId, IGearEnergyTransformer> _registeredGears = new();
        private readonly HashSet<IGearOverloadTickTarget> _overloadTickTargets = new();
        private GearNetworkTopologyMap _topologyMap;
        private HashSet<GearNetwork> _networksRequiringRecalc;
        private HashSet<GearNetwork> _continuousTickNetworks;
        private bool _isTopologyDirty = true;

        public GearNetworkDatastore()
        {
            _topologyMap = GearNetworkTopologyMap.CreateEmpty();
            _networksRequiringRecalc = new HashSet<GearNetwork>();
            _continuousTickNetworks = new HashSet<GearNetwork>();
        }

        internal IReadOnlyCollection<GearNetwork> ContinuousTickNetworks => _continuousTickNetworks;

        public void AddGear(IGearEnergyTransformer gear)
        {
            _registeredGears[gear.BlockInstanceId] = gear;
            _isTopologyDirty = true;
        }

        public void RemoveGear(IGearEnergyTransformer gear)
        {
            _registeredGears.Remove(gear.BlockInstanceId);
            _isTopologyDirty = true;
        }

        public void MarkTopologyDirty()
        {
            _isTopologyDirty = true;
        }

        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;

            // live gearから全体を作り直し、旧topologyを破棄する
            // Rebuild everything from live gears, then discard the old topology
            var rebuilt = GearNetworkTopologyMap.Build(_registeredGears.Values);
            var previousTopologyMap = _topologyMap;

            _topologyMap = rebuilt.TopologyMap;
            _networksRequiringRecalc = rebuilt.NetworksRequiringRecalc;
            _continuousTickNetworks = rebuilt.ContinuousTickNetworks;

            previousTopologyMap.Destroy();
            _isTopologyDirty = false;
        }

        public void RegisterOverloadTickTarget(IGearOverloadTickTarget target)
        {
            _overloadTickTargets.Add(target);
        }

        public void UnregisterOverloadTickTarget(IGearOverloadTickTarget target)
        {
            _overloadTickTargets.Remove(target);
        }

        internal void CollectOverloadTickTargets(List<IGearOverloadTickTarget> buffer)
        {
            buffer.AddRange(_overloadTickTargets);
        }

        public void NotifyGeneratorOutputChanged(IGearEnergyTransformer generator)
        {
            AddAppliedNetworkToRecalculation(generator);
        }

        public void NotifyConsumerDemandChanged(IGearEnergyTransformer consumer)
        {
            AddAppliedNetworkToRecalculation(consumer);
        }

        public bool TryGetGearNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            return _topologyMap.TryGetNetwork(blockInstanceId, out network);
        }

        public GearNetwork GetGearNetwork(BlockInstanceId blockInstanceId)
        {
            return _topologyMap.GetNetwork(blockInstanceId);
        }

        internal void CollectNetworksRequiringRecalc(List<GearNetwork> buffer)
        {
            buffer.AddRange(_networksRequiringRecalc);
            _networksRequiringRecalc.Clear();
        }

        private void AddAppliedNetworkToRecalculation(IGearEnergyTransformer gear)
        {
            if (_topologyMap.TryGetNetwork(gear.BlockInstanceId, out var network))
                _networksRequiringRecalc.Add(network);
        }
    }
}
