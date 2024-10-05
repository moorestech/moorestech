using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using UniRx;
using Random = System.Random;

namespace Game.Gear.Common
{
    public class GearNetworkDatastore
    {
        private static GearNetworkDatastore _instance;
        
        private readonly Dictionary<BlockInstanceId, GearNetwork> _blockEntityToGearNetwork; // key ブロックのEntityId value そのブロックが所属するNW
        private readonly Dictionary<GearNetworkId, GearNetwork> _gearNetworks = new();
        private readonly Random _random = new(215180);
        
        public GearNetworkDatastore()
        {
            _instance = this;
            _blockEntityToGearNetwork = new Dictionary<BlockInstanceId, GearNetwork>();
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public IReadOnlyDictionary<GearNetworkId, GearNetwork> GearNetworks => _gearNetworks;
        
        public static void AddGear(IGearEnergyTransformer gear)
        {
            _instance.AddGearInternal(gear);
        }
        
        private void AddGearInternal(IGearEnergyTransformer gear)
        {
            var connectedNetworkIds = new HashSet<GearNetworkId>();
            foreach (var connectedGear in gear.GetGearConnects())
                //新しく設置された歯車に接続している歯車は、すべて既存のNWに接続している前提
                if (_blockEntityToGearNetwork.ContainsKey(connectedGear.Transformer.BlockInstanceId))
                {
                    var networkId = _blockEntityToGearNetwork[connectedGear.Transformer.BlockInstanceId].NetworkId;
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
                var networkId = GearNetworkId.CreateNetworkId();
                var network = new GearNetwork(networkId);
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
                _gearNetworks.Add(networkId, network);
            }
            
            void ConnectNetwork()
            {
                var networkId = connectedNetworkIds.First();
                var network = _gearNetworks[networkId];
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
            }
            
            void MergeNetworks()
            {
                // マージのために歯車を取得
                var transformers = new List<IGearEnergyTransformer>();
                var generators = new List<IGearGenerator>();
                
                foreach (var networkId in connectedNetworkIds.ToList())
                {
                    var network = _gearNetworks[networkId];
                    transformers.AddRange(network.GearTransformers);
                    generators.AddRange(network.GearGenerators);
                    _gearNetworks.Remove(networkId);
                }
                
                var newNetworkId = GearNetworkId.CreateNetworkId();
                var newNetwork = new GearNetwork(newNetworkId);
                
                foreach (var transformer in transformers) newNetwork.AddGear(transformer);
                foreach (var generator in generators) newNetwork.AddGear(generator);
                
                transformers.Add(gear);
                newNetwork.AddGear(gear);
                _blockEntityToGearNetwork[gear.BlockInstanceId] = newNetwork;
                
                // マージしたNWに所属する歯車のNWを更新
                for (var i = 0; i < _blockEntityToGearNetwork.Keys.Count; i++)
                {
                    var pair = _blockEntityToGearNetwork.ElementAt(i);
                    if (connectedNetworkIds.Contains(pair.Value.NetworkId)) _blockEntityToGearNetwork[pair.Key] = newNetwork;
                }
                
                _gearNetworks.Add(newNetworkId, newNetwork);
                foreach (var removeNetworkId in connectedNetworkIds) _gearNetworks.Remove(removeNetworkId);
            }
            
            #endregion
        }
        
        public static void RemoveGear(IGearEnergyTransformer gear)
        {
            // 自身をnetworkから削除
            var network = _instance._blockEntityToGearNetwork[gear.BlockInstanceId];
            network.RemoveGear(gear);
            
            //接続していた歯車ネットワークをデータベースから破棄
            _instance._gearNetworks.Remove(network.NetworkId);
            
            //gearに接続されている全てのgearをblockEntityToGearNetworkから削除
            var gearStack = new Stack<IGearEnergyTransformer>();
            gearStack.Push(gear);
            while (gearStack.TryPop(out var stackGear))
            {
                _instance._blockEntityToGearNetwork.Remove(stackGear.BlockInstanceId);
                
                foreach (var connectedGear in stackGear.GetGearConnects())
                    if (_instance._blockEntityToGearNetwork.ContainsKey(connectedGear.Transformer.BlockInstanceId))
                        gearStack.Push(connectedGear.Transformer);
            }
            
            //もともと接続していたブロックをすべてAddする
            var transformers = network.GearTransformers;
            var generators = network.GearGenerators;
            
            //重くなったらアルゴリズムを変える
            foreach (var transformer in transformers) AddGear(transformer);
            foreach (var generator in generators) AddGear(generator);
        }
        
        private void Update()
        {
            foreach (var gearNetwork in _gearNetworks.Values) // TODO パフォーマンスがやばくなったらやめる
                gearNetwork.ManualUpdate();
        }
    }
}