using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // dirty時に電力mapを原子交換する
    // Atomically swaps in a newly built applied map only when the live registration graph is dirty
    public class ElectricWireNetworkDatastore : IElectricWireNetworkLookup, IElectricWireNetworkMutation
    {
        private readonly Dictionary<BlockInstanceId, IElectricWireConnector> _registeredConnectors = new();
        private ElectricWireTopologyMap _topologyMap = ElectricWireTopologyMap.CreateEmpty();
        private bool _isTopologyDirty = true;
        private bool _isStatisticsDirty = true;

        public bool IsDerivedStateDirty => _isTopologyDirty || _isStatisticsDirty;

        public void AddConnector(IElectricWireConnector connector)
        {
            _registeredConnectors[connector.BlockInstanceId] = connector;
            MarkTopologyDirty();
        }

        public void RemoveConnector(IElectricWireConnector connector)
        {
            _registeredConnectors.Remove(connector.BlockInstanceId);
            MarkTopologyDirty();
        }

        public void MarkTopologyDirty()
        {
            _isTopologyDirty = true;
            _isStatisticsDirty = true;
        }

        public void MarkStatisticsDirty()
        {
            _isStatisticsDirty = true;
        }

        public void MarkStatisticsSettled()
        {
            _isStatisticsDirty = false;
        }

        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;

            // 新しい電力mapを全体構築してから参照を交換する
            // Build the complete electric map before swapping its reference
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
