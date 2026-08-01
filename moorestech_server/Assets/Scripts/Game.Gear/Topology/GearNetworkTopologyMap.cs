using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Common;

namespace Game.Gear.Topology
{
    // tick境界のgear所属mapを保持する
    // Holds gear connected components and ownership applied at one tick boundary
    internal class GearNetworkTopologyMap
    {
        private readonly Dictionary<BlockInstanceId, GearNetwork> _blockEntityToGearNetwork;
        private readonly Dictionary<GearNetworkId, GearNetwork> _gearNetworks;

        private GearNetworkTopologyMap(
            Dictionary<BlockInstanceId, GearNetwork> blockEntityToGearNetwork,
            Dictionary<GearNetworkId, GearNetwork> gearNetworks)
        {
            _blockEntityToGearNetwork = blockEntityToGearNetwork;
            _gearNetworks = gearNetworks;
        }

        public static GearNetworkTopologyMap CreateEmpty()
        {
            return new GearNetworkTopologyMap(
                new Dictionary<BlockInstanceId, GearNetwork>(),
                new Dictionary<GearNetworkId, GearNetwork>());
        }

        public static GearNetworkTopologyBuildResult Build(ICollection<IGearEnergyTransformer> registeredGears)
        {
            // live頂点をID昇順の正準順に並べ、BFS入力を作る（生成順を登録履歴非依存にする）
            // Sort live vertices by ID into canonical order so build results never depend on registration history
            var remaining = new IGearEnergyTransformer[registeredGears.Count];
            registeredGears.CopyTo(remaining, 0);
            Array.Sort(remaining, (a, b) => a.BlockInstanceId.CompareTo(b.BlockInstanceId));
            var idToIndex = new Dictionary<BlockInstanceId, int>(registeredGears.Count);
            for (var i = 0; i < remaining.Length; i++) idToIndex.Add(remaining[i].BlockInstanceId, i);

            // 連結成分ごとに新しいgear網を作る
            // Create fresh gear networks and runtime collections for each connected component
            var components = GearConnectedComponentFinder.FindComponents(remaining, idToIndex);
            var gearToNetwork = new Dictionary<BlockInstanceId, GearNetwork>(registeredGears.Count);
            var networks = new Dictionary<GearNetworkId, GearNetwork>(components.Count);
            var recalculationNetworks = new HashSet<GearNetwork>();
            var continuousTickNetworks = new HashSet<GearNetwork>();
            foreach (var component in components)
            {
                // 新gear網は回転cacheを持たない
                // A fresh network references no old rotation cache; normal calculation performs its first traversal this tick
                var network = new GearNetwork(GearNetworkId.CreateNetworkId());
                foreach (var gear in component)
                {
                    network.AddGear(gear);
                    gearToNetwork.Add(gear.BlockInstanceId, network);
                }

                networks.Add(network.NetworkId, network);
                recalculationNetworks.Add(network);
                if (network.HasContinuousTickGenerator) continuousTickNetworks.Add(network);
            }

            var topologyMap = new GearNetworkTopologyMap(gearToNetwork, networks);
            return new GearNetworkTopologyBuildResult(
                topologyMap,
                recalculationNetworks,
                continuousTickNetworks);
        }

        public bool TryGetNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            return _blockEntityToGearNetwork.TryGetValue(blockInstanceId, out network);
        }

        public GearNetwork GetNetwork(BlockInstanceId blockInstanceId)
        {
            return _blockEntityToGearNetwork[blockInstanceId];
        }

        public void Destroy()
        {
            foreach (var network in _gearNetworks.Values) network.Destroy();
            _blockEntityToGearNetwork.Clear();
            _gearNetworks.Clear();
        }
    }
}
