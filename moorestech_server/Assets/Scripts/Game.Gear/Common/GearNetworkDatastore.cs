using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using UniRx;

namespace Game.Gear.Common
{
    public class GearNetworkDatastore
    {
        private readonly List<GearNetwork> _gearNetworks;
        private readonly Dictionary<int, GearNetwork> _blockEntityToGearNetwork;

        private readonly Random _random = new(215180);

        public GearNetworkDatastore()
        {
            _blockEntityToGearNetwork = new Dictionary<int, GearNetwork>();
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        public void AddGear(IGear gear)
        {
            var connectedNetworkIds = new HashSet<int>();
            foreach (var connectedGear in gear.ConnectingTransformers)
            {
                //新しく設置された歯車に接続している歯車は、すべて既存のNWに接続している前提
                var networkId = _blockEntityToGearNetwork[connectedGear.EntityId].NetworkId;
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
                _gearNetworks.Add(network);
            }

            void ConnectNetwork()
            {
                var networkId = connectedNetworkIds.First();
                var network = _blockEntityToGearNetwork[networkId];
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.EntityId, network);
            }

            void MergeNetworks()
            {
                var consumers = new List<IGearConsumer>();
                var generators = new List<IGearGenerator>();

                foreach (var networkId in connectedNetworkIds.ToList())
                {
                    var network = _blockEntityToGearNetwork[networkId];
                    consumers.AddRange(network.GearConsumers);
                    generators.AddRange(network.GearGenerators);
                    _blockEntityToGearNetwork.Remove(networkId);
                }

                var newNetworkId = _random.Next(int.MinValue, int.MaxValue);
                var newNetwork = new GearNetwork(newNetworkId);

                foreach (var consumer in consumers)
                {
                    newNetwork.AddGear(consumer);
                }
                foreach (var generator in generators)
                {
                    newNetwork.AddGear(generator);
                }

                for (int i = 0; i < _blockEntityToGearNetwork.Keys.Count; i++)
                {
                    var key = _blockEntityToGearNetwork.ElementAt(i).Key;
                    if (connectedNetworkIds.Contains(key))
                    {
                        _blockEntityToGearNetwork[key] = newNetwork;
                    }
                }

                _gearNetworks.Add(newNetwork);
                for (int i = _gearNetworks.Count - 1; i >= 0; i--)
                {
                    if (connectedNetworkIds.Contains(_gearNetworks[i].NetworkId))
                    {
                        _gearNetworks.RemoveAt(i);
                    }
                }
            }

            #endregion
        }

        private void Update()
        {
            foreach (var gearNetwork in _gearNetworks)
            {
                gearNetwork.CalculateSupplyPower();
            }
        }
    }
}