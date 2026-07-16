using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // dirty時に電力mapを原子交換する
    // Atomically swaps in a newly built applied map only when the live registration graph is dirty
    public class ElectricWireNetworkDatastore : IElectricWireNetworkDatastore
    {
        private readonly Dictionary<BlockInstanceId, IElectricWireConnector> _registeredConnectors = new();
        private ElectricWireTopologyMap _topologyMap = ElectricWireTopologyMap.CreateEmpty();
        private bool _isTopologyDirty = true;

        public void AddConnector(IElectricWireConnector connector)
        {
            _registeredConnectors[connector.BlockInstanceId] = connector;
            _isTopologyDirty = true;
        }

        public void RemoveConnector(IElectricWireConnector connector)
        {
            _registeredConnectors.Remove(connector.BlockInstanceId);
            _isTopologyDirty = true;
        }

        public void MarkTopologyDirty()
        {
            _isTopologyDirty = true;
        }

        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;

            // 完成後にだけ電力mapを交換する
            // Leave the applied map untouched until construction succeeds, then swap its reference
            var rebuilt = ElectricWireTopologyMap.Build(_registeredConnectors.Values);
            var previous = _topologyMap;
            _topologyMap = rebuilt;
            previous.Destroy();
            _isTopologyDirty = false;
        }

        public bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment)
        {
            return _topologyMap.TryGetEnergySegment(blockInstanceId, out segment);
        }

        public IReadOnlyList<EnergySegment> GetSegments()
        {
            return _topologyMap.GetSegments();
        }
    }
}
