using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using UniRx;

namespace Game.Gear.Common
{
    public class GearNetworkDatastore
    {
        private static GearNetworkDatastore _instance;

        private readonly Dictionary<int, GearNetwork> _blockEntityToGearNetwork; // key ブロックのEntityId value そのブロックが所属するNW
        private readonly Dictionary<int, GearNetwork> _gearNetworks = new();
        private readonly Random _random = new(215180);

        public GearNetworkDatastore()
        {
            _instance = this;
            _blockEntityToGearNetwork = new Dictionary<int, GearNetwork>();
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        public IReadOnlyDictionary<int, GearNetwork> GearNetworks => _gearNetworks;

        public static void AddGear(IGearEnergyTransformer gear)
        {
            _instance.AddGearInternal(gear);
        }

        private void AddGearInternal(IGearEnergyTransformer gear)
        {
            var connectedNetworkIds = new HashSet<int>();
            foreach (var connectedGear in gear.Connects)
            {
                //新しく設置された歯車に接続している歯車は、すべて既存のNWに接続している前提
                var networkId = _blockEntityToGearNetwork[connectedGear.Transformer.EntityId].NetworkId;
                connectedNetworkIds.Add(networkId);
            }

            //接続しているNWが1つもない場合は新規NWを作成
            switch (connectedNetworkIds.Count)
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
                var networkId = _random.Next(int.MinValue, int.MaxValue);
                var network = new GearNetwork(networkId);
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.EntityId, network);
                _gearNetworks.Add(networkId, network);
            }

            void ConnectNetwork()
            {
                var networkId = connectedNetworkIds.First();
                var network = _gearNetworks[networkId];
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.EntityId, network);
            }

            void MergeNetworks()
            {
                // マージのために歯車を取得
                var transformers = new List<IGearEnergyTransformer>();
                var generators = new List<IGearGenerator>();

                foreach (var networkId in connectedNetworkIds.ToList())
                {
                    var network = _blockEntityToGearNetwork[networkId];
                    transformers.AddRange(network.GearTransformers);
                    generators.AddRange(network.GearGenerators);
                    _blockEntityToGearNetwork.Remove(networkId);
                }

                var newNetworkId = _random.Next(int.MinValue, int.MaxValue);
                var newNetwork = new GearNetwork(newNetworkId);

                foreach (var transformer in transformers)
                {
                    newNetwork.AddGear(transformer);
                }
                foreach (var generator in generators)
                {
                    newNetwork.AddGear(generator);
                }

                // マージしたNWに所属する歯車のNWを更新
                for (var i = 0; i < _blockEntityToGearNetwork.Keys.Count; i++)
                {
                    var key = _blockEntityToGearNetwork.ElementAt(i).Key;
                    if (connectedNetworkIds.Contains(key))
                    {
                        _blockEntityToGearNetwork[key] = newNetwork;
                    }
                }

                _gearNetworks.Add(newNetworkId, newNetwork);
                foreach (var removeNetworkId in connectedNetworkIds)
                {
                    _gearNetworks.Remove(removeNetworkId);
                }
            }

            #endregion
        }

        public static void RemoveGear(IGearEnergyTransformer gear)
        {
            //接続していた歯車ネットワークを破棄
            var network = _instance._blockEntityToGearNetwork[gear.EntityId];
            network.RemoveGear(gear);
            _instance._blockEntityToGearNetwork[gear.EntityId] = null;
            
            //もともと接続していたブロックをすべてAddする
            var transformers = network.GearTransformers;
            var generators = network.GearGenerators;
            
            //重くなったらアルゴリズムを変える
            foreach (var transformer in transformers)
            {
                AddGear(transformer);
            }
            foreach (var generator in generators)
            {
                AddGear(generator);
            }
        }

        private void Update()
        {
            foreach (var gearNetwork in _gearNetworks.Values) // TODO パフォーマンスがやばくなったらやめる
            {
                gearNetwork.ManualUpdate();
            }
        }
    }
}