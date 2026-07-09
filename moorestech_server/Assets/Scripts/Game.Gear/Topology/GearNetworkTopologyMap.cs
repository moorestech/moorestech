using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Gear.Common;

namespace Game.Gear.Topology
{
    // gearの所属networkマップを保持し、追加/削除に伴う作成・併合・分割を実行する
    // Holds the gear-to-network map and performs create/merge/split when gears are added or removed
    public class GearNetworkTopologyMap
    {
        private readonly Dictionary<BlockInstanceId, GearNetwork> _blockEntityToGearNetwork = new();
        private readonly Dictionary<GearNetworkId, GearNetwork> _gearNetworks = new();

        // topology変化したnetworkと破棄されたnetworkを所有者（datastore）へ通知するコールバック
        // Callbacks notifying the owner (datastore) of changed and discarded networks
        private readonly Action<GearNetwork> _onNetworkChanged;
        private readonly Action<GearNetwork> _onNetworkDiscarded;

        public GearNetworkTopologyMap(Action<GearNetwork> onNetworkChanged, Action<GearNetwork> onNetworkDiscarded)
        {
            _onNetworkChanged = onNetworkChanged;
            _onNetworkDiscarded = onNetworkDiscarded;
        }

        public bool TryGetNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            return _blockEntityToGearNetwork.TryGetValue(blockInstanceId, out network);
        }

        public void AddGear(IGearEnergyTransformer gear)
        {
            // 接続先gearの所属networkを重複なく集める
            // Collect the owning networks of neighbors without duplicates
            var connectedNetworks = new HashSet<GearNetwork>();
            foreach (var connectedGear in gear.GetGearConnects())
                if (_blockEntityToGearNetwork.TryGetValue(connectedGear.Transformer.BlockInstanceId, out var neighborNetwork))
                    connectedNetworks.Add(neighborNetwork);

            switch (connectedNetworks.Count)
            {
                case 0:
                    CreateNetwork();
                    break;
                case 1:
                    ConnectNetwork();
                    break;
                default:
                    MergeNetworks();
                    break;
            }

            #region Internal

            void CreateNetwork()
            {
                var network = new GearNetwork(GearNetworkId.CreateNetworkId());
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
                _gearNetworks.Add(network.NetworkId, network);
                _onNetworkChanged(network);
            }

            void ConnectNetwork()
            {
                var network = connectedNetworks.First();
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
                _onNetworkChanged(network);
            }

            void MergeNetworks()
            {
                // Union-by-size: 最大networkを吸収側とし、残りの全gearを流し込んでマッピング更新を最小化する
                // Union-by-size: fold every other network into the largest one, minimizing mapping updates
                GearNetwork largest = null;
                var largestSize = 0;
                foreach (var candidate in connectedNetworks)
                {
                    var size = candidate.GearTransformers.Count + candidate.GearGenerators.Count;
                    if (largest == null || size > largestSize)
                    {
                        largestSize = size;
                        largest = candidate;
                    }
                }

                foreach (var absorbed in connectedNetworks)
                {
                    if (absorbed == largest) continue;
                    foreach (var transformer in absorbed.GearTransformers)
                    {
                        largest.AddGear(transformer);
                        _blockEntityToGearNetwork[transformer.BlockInstanceId] = largest;
                    }
                    foreach (var generator in absorbed.GearGenerators)
                    {
                        largest.AddGear(generator);
                        _blockEntityToGearNetwork[generator.BlockInstanceId] = largest;
                    }
                    DiscardNetwork(absorbed);
                }

                largest.AddGear(gear);
                _blockEntityToGearNetwork[gear.BlockInstanceId] = largest;
                _onNetworkChanged(largest);
            }

            #endregion
        }

        public void RemoveGear(IGearEnergyTransformer gear)
        {
            // 所属networkから自身を除去する。未登録なら何もしない
            // Remove the gear from its owning network; do nothing when unregistered
            if (!_blockEntityToGearNetwork.TryGetValue(gear.BlockInstanceId, out var network)) return;
            _blockEntityToGearNetwork.Remove(gear.BlockInstanceId);
            network.RemoveGear(gear);

            var totalCount = network.GearTransformers.Count + network.GearGenerators.Count;
            if (totalCount == 0)
            {
                DiscardNetwork(network);
                return;
            }

            // 残存gearの配列とid→indexマップを構築し、連結成分へ分解する
            // Build the surviving gear array and id→index map, then decompose into connected components
            var remaining = new IGearEnergyTransformer[totalCount];
            var idToIndex = new Dictionary<BlockInstanceId, int>(totalCount);
            var fillIndex = 0;
            foreach (var transformer in network.GearTransformers)
            {
                remaining[fillIndex] = transformer;
                idToIndex[transformer.BlockInstanceId] = fillIndex;
                fillIndex++;
            }
            foreach (var generator in network.GearGenerators)
            {
                remaining[fillIndex] = generator;
                idToIndex[generator.BlockInstanceId] = fillIndex;
                fillIndex++;
            }

            var components = GearConnectedComponentFinder.FindComponents(remaining, idToIndex);

            // 分断なしなら既存networkを維持し、変化通知のみ行う
            // No split: keep the network and only notify the change
            if (components.Count == 1)
            {
                _onNetworkChanged(network);
                return;
            }

            // 複数成分へ分断されたので既存networkを破棄し、成分ごとに新networkを生成する
            // The network split into components: discard it and create one new network per component
            DiscardNetwork(network);
            foreach (var component in components)
            {
                var newNetwork = new GearNetwork(GearNetworkId.CreateNetworkId());
                foreach (var member in component)
                {
                    newNetwork.AddGear(member);
                    _blockEntityToGearNetwork[member.BlockInstanceId] = newNetwork;
                }
                _gearNetworks.Add(newNetwork.NetworkId, newNetwork);
                _onNetworkChanged(newNetwork);
            }
        }

        private void DiscardNetwork(GearNetwork network)
        {
            _gearNetworks.Remove(network.NetworkId);
            _onNetworkDiscarded(network);
        }
    }
}
