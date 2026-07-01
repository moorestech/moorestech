using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public class GearNetworkDatastore
    {
        private static GearNetworkDatastore _instance;
        private readonly Dictionary<BlockInstanceId, GearNetwork> _blockEntityToGearNetwork = new();
        private readonly Dictionary<GearNetworkId, GearNetwork> _gearNetworks = new();

        public GearNetworkDatastore()
        {
            _instance = this;
        }

        public IReadOnlyDictionary<GearNetworkId, GearNetwork> GearNetworks => _gearNetworks;

        public static void AddGear(IGearEnergyTransformer gear)
        {
            _instance.AddGearInternal(gear);
        }

        public static void RemoveGear(IGearEnergyTransformer gear)
        {
            _instance.RemoveGearInternal(gear);
        }

        public static GearNetwork GetGearNetwork(BlockInstanceId blockInstanceId)
        {
            return _instance._blockEntityToGearNetwork[blockInstanceId];
        }

        public static bool TryGetGearNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            return _instance._blockEntityToGearNetwork.TryGetValue(blockInstanceId, out network);
        }

        public void UpdateAllNetworks(GearDemandSnapshotStore demandSnapshotStore, GearRuntimeStateStore runtimeStateStore)
        {
            foreach (var gearNetwork in _gearNetworks.Values)
            {
                gearNetwork.Update(demandSnapshotStore, runtimeStateStore);
            }
        }

        private void AddGearInternal(IGearEnergyTransformer gear)
        {
            var connectedNetworks = CollectConnectedNetworks(gear);
            if (connectedNetworks.Count == 0)
            {
                CreateNetwork(gear);
                return;
            }

            if (connectedNetworks.Count == 1)
            {
                AddToSingleNetwork(gear, connectedNetworks);
                return;
            }

            MergeNetworks(gear, connectedNetworks);
        }

        private void RemoveGearInternal(IGearEnergyTransformer gear)
        {
            if (!_blockEntityToGearNetwork.TryGetValue(gear.BlockInstanceId, out var network)) return;

            _blockEntityToGearNetwork.Remove(gear.BlockInstanceId);
            network.RemoveGear(gear);
            var remainingCount = network.GearTransformers.Count + network.GearGenerators.Count;
            if (remainingCount == 0)
            {
                _gearNetworks.Remove(network.NetworkId);
                return;
            }

            // 削除後の連結成分が1つなら、既存networkをそのまま使う。
            // If the removal leaves one component, keep the current network.
            var remaining = BuildRemainingArray(network, remainingCount, out var idToIdx);
            var components = GearNetworkComponentFinder.FindComponents(remaining, idToIdx);
            if (components.Count == 1) return;
            SplitNetwork(network, components);
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
            _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
            _gearNetworks.Add(networkId, network);
        }

        private void AddToSingleNetwork(IGearEnergyTransformer gear, HashSet<GearNetwork> connectedNetworks)
        {
            GearNetwork network = null;
            foreach (var connectedNetwork in connectedNetworks)
            {
                network = connectedNetwork;
                break;
            }
            network.AddGear(gear);
            _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
        }

        private void MergeNetworks(IGearEnergyTransformer gear, HashSet<GearNetwork> connectedNetworks)
        {
            var largest = FindLargestNetwork(connectedNetworks);
            foreach (var network in connectedNetworks)
            {
                if (network == largest) continue;
                MoveNetworkMembers(network, largest);
                _gearNetworks.Remove(network.NetworkId);
            }

            largest.AddGear(gear);
            _blockEntityToGearNetwork[gear.BlockInstanceId] = largest;
        }

        private static GearNetwork FindLargestNetwork(HashSet<GearNetwork> networks)
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

        private void MoveNetworkMembers(GearNetwork source, GearNetwork destination)
        {
            foreach (var transformer in source.GearTransformers)
            {
                destination.AddGear(transformer);
                _blockEntityToGearNetwork[transformer.BlockInstanceId] = destination;
            }

            foreach (var generator in source.GearGenerators)
            {
                destination.AddGear(generator);
                _blockEntityToGearNetwork[generator.BlockInstanceId] = destination;
            }
        }

        private static IGearEnergyTransformer[] BuildRemainingArray(GearNetwork network, int count, out Dictionary<BlockInstanceId, int> idToIdx)
        {
            var remaining = new IGearEnergyTransformer[count];
            idToIdx = new Dictionary<BlockInstanceId, int>(count);
            var index = 0;
            FillRemaining(network.GearTransformers, remaining, idToIdx, ref index);
            FillRemaining(network.GearGenerators, remaining, idToIdx, ref index);
            return remaining;
        }
        private static void FillRemaining(IReadOnlyList<IGearEnergyTransformer> gears, IGearEnergyTransformer[] remaining, Dictionary<BlockInstanceId, int> idToIdx, ref int index)
        {
            foreach (var gear in gears)
            {
                remaining[index] = gear;
                idToIdx[gear.BlockInstanceId] = index;
                index++;
            }
        }

        private void SplitNetwork(GearNetwork oldNetwork, List<List<IGearEnergyTransformer>> components)
        {
            _gearNetworks.Remove(oldNetwork.NetworkId);
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
}
