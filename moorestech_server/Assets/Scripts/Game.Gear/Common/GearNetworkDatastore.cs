using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public class GearNetworkDatastore
    {
        private static GearNetworkDatastore _instance;

        private readonly Dictionary<BlockInstanceId, GearNetwork> _blockEntityToGearNetwork = new();
        private readonly Dictionary<GearNetworkId, GearNetwork> _gearNetworks = new();
        private readonly List<GearTopologyMutation> _pendingMutations = new();

        public IReadOnlyDictionary<GearNetworkId, GearNetwork> GearNetworks => _gearNetworks;

        public GearNetworkDatastore()
        {
            _instance = this;
        }

        public static void AddGear(IGearEnergyTransformer gear)
        {
            _instance._pendingMutations.Add(new GearTopologyMutation(gear, true));
        }

        public static void RemoveGear(IGearEnergyTransformer gear)
        {
            _instance._pendingMutations.Add(new GearTopologyMutation(gear, false));
        }

        internal void ApplyTopologyMutations(GearDemandSnapshotStore demandStore, GearRuntimeStateStore runtimeStore)
        {
            if (_pendingMutations.Count == 0) return;

            // tick開始時点のmutationだけを確定し、追加分は次tickへ送る。
            // Fix mutations at tick start and leave newly queued ones for the next tick.
            var applyingMutations = new List<GearTopologyMutation>(_pendingMutations);
            _pendingMutations.Clear();
            foreach (var mutation in applyingMutations)
            {
                if (mutation.IsAdd) AddGearInternal(mutation.Gear, runtimeStore);
                else RemoveGearInternal(mutation.Gear, demandStore, runtimeStore);
            }
        }

        internal void UpdateNetworks(GearDemandSnapshotStore demandStore, GearRuntimeStateStore runtimeStore)
        {
            foreach (var network in _gearNetworks.Values)
            {
                network.UpdateNetwork(demandStore, runtimeStore);
            }
        }

        public static GearNetwork GetGearNetwork(BlockInstanceId blockInstanceId)
        {
            return _instance._blockEntityToGearNetwork[blockInstanceId];
        }

        public static bool TryGetGearNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            return _instance._blockEntityToGearNetwork.TryGetValue(blockInstanceId, out network);
        }

        private void AddGearInternal(IGearEnergyTransformer gear, GearRuntimeStateStore runtimeStore)
        {
            if (_blockEntityToGearNetwork.ContainsKey(gear.BlockInstanceId)) return;
            var connectedNetworks = CollectConnectedNetworks(gear);
            if (connectedNetworks.Count == 0) CreateNetwork(gear);
            else if (connectedNetworks.Count == 1) ConnectNetwork(gear, connectedNetworks);
            else MergeNetworks(gear, connectedNetworks, runtimeStore);
        }

        private HashSet<GearNetwork> CollectConnectedNetworks(IGearEnergyTransformer gear)
        {
            var connectedNetworks = new HashSet<GearNetwork>();
            foreach (var connectedGear in gear.GetGearConnects())
            {
                if (_blockEntityToGearNetwork.TryGetValue(connectedGear.Transformer.BlockInstanceId, out var network))
                {
                    connectedNetworks.Add(network);
                }
            }
            return connectedNetworks;
        }

        private void CreateNetwork(IGearEnergyTransformer gear)
        {
            var networkId = GearNetworkId.CreateNetworkId();
            var network = new GearNetwork(networkId);
            network.AddGear(gear);
            _gearNetworks.Add(networkId, network);
            _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
        }

        private void ConnectNetwork(IGearEnergyTransformer gear, HashSet<GearNetwork> connectedNetworks)
        {
            GearNetwork network = null;
            foreach (var candidate in connectedNetworks) network = candidate;
            network.AddGear(gear);
            _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
        }

        private void MergeNetworks(IGearEnergyTransformer gear, HashSet<GearNetwork> connectedNetworks, GearRuntimeStateStore runtimeStore)
        {
            var largest = SelectLargestNetwork(connectedNetworks);
            foreach (var network in connectedNetworks)
            {
                if (network == largest) continue;
                MoveNetworkMembers(network, largest);
                _gearNetworks.Remove(network.NetworkId);
                runtimeStore.RemoveNetwork(network.NetworkId);
            }
            largest.AddGear(gear);
            _blockEntityToGearNetwork[gear.BlockInstanceId] = largest;
        }

        private GearNetwork SelectLargestNetwork(HashSet<GearNetwork> networks)
        {
            GearNetwork largest = null;
            var largestSize = 0;
            foreach (var network in networks)
            {
                var size = network.GearTransformers.Count + network.GearGenerators.Count;
                if (largest != null && size <= largestSize) continue;
                largest = network;
                largestSize = size;
            }
            return largest;
        }

        private void MoveNetworkMembers(GearNetwork from, GearNetwork to)
        {
            foreach (var transformer in from.GearTransformers)
            {
                to.AddGear(transformer);
                _blockEntityToGearNetwork[transformer.BlockInstanceId] = to;
            }
            foreach (var generator in from.GearGenerators)
            {
                to.AddGear(generator);
                _blockEntityToGearNetwork[generator.BlockInstanceId] = to;
            }
        }

        private void RemoveGearInternal(IGearEnergyTransformer gear, GearDemandSnapshotStore demandStore, GearRuntimeStateStore runtimeStore)
        {
            if (!_blockEntityToGearNetwork.TryGetValue(gear.BlockInstanceId, out var network)) return;
            _blockEntityToGearNetwork.Remove(gear.BlockInstanceId);
            demandStore.Remove(gear.BlockInstanceId);
            runtimeStore.RemoveGear(gear.BlockInstanceId);
            network.RemoveGear(gear);

            var totalCount = network.GearTransformers.Count + network.GearGenerators.Count;
            if (totalCount == 0)
            {
                _gearNetworks.Remove(network.NetworkId);
                runtimeStore.RemoveNetwork(network.NetworkId);
                return;
            }

            var components = GearNetworkComponentFinder.FindComponents(network);
            if (components.Count == 1) return;
            SplitNetwork(network, components, runtimeStore);
        }

        private void SplitNetwork(GearNetwork oldNetwork, List<List<IGearEnergyTransformer>> components, GearRuntimeStateStore runtimeStore)
        {
            _gearNetworks.Remove(oldNetwork.NetworkId);
            runtimeStore.RemoveNetwork(oldNetwork.NetworkId);
            foreach (var component in components)
            {
                var newNetworkId = GearNetworkId.CreateNetworkId();
                var newNetwork = new GearNetwork(newNetworkId);
                foreach (var gear in component)
                {
                    newNetwork.AddGear(gear);
                    _blockEntityToGearNetwork[gear.BlockInstanceId] = newNetwork;
                }
                _gearNetworks.Add(newNetworkId, newNetwork);
            }
        }
    }

    public readonly struct GearTopologyMutation
    {
        public readonly IGearEnergyTransformer Gear;
        public readonly bool IsAdd;

        public GearTopologyMutation(IGearEnergyTransformer gear, bool isAdd)
        {
            Gear = gear;
            IsAdd = isAdd;
        }
    }
}
