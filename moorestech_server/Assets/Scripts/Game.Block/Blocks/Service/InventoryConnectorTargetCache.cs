using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Service
{
    internal class InventoryConnectorTargetCache
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private KeyValuePair<IBlockInventory, ConnectedInfo>[] _targets = new KeyValuePair<IBlockInventory, ConnectedInfo>[0];

        public InventoryConnectorTargetCache(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }

        public KeyValuePair<IBlockInventory, ConnectedInfo>[] GetTargets()
        {
            var connectedTargets = _blockConnectorComponent.ConnectedTargets;
            if (_targets.Length != connectedTargets.Count || HasDifferentTargets(connectedTargets))
            {
                Rebuild(connectedTargets);
            }

            return _targets;
        }

        public int Count => GetTargets().Length;

        private bool HasDifferentTargets(IReadOnlyDictionary<IBlockInventory, ConnectedInfo> connectedTargets)
        {
            // テストや配置変更で同数の接続先が入れ替わった場合もcacheを張り直す
            // Rebuild when targets changed even if the connected target count stayed the same.
            foreach (var target in _targets)
            {
                if (!connectedTargets.ContainsKey(target.Key)) return true;
            }

            return false;
        }

        private void Rebuild(IReadOnlyDictionary<IBlockInventory, ConnectedInfo> connectedTargets)
        {
            var index = 0;
            _targets = new KeyValuePair<IBlockInventory, ConnectedInfo>[connectedTargets.Count];
            foreach (var target in connectedTargets)
            {
                _targets[index] = target;
                index++;
            }
        }
    }
}
