using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // live電線と適用済みsegmentを分離する
    // Separates live wire-graph registration from derived segments applied at the tick boundary
    public interface IElectricWireNetworkDatastore
    {
        bool IsTopologyDirty { get; }

        void AddConnector(IElectricWireConnector connector);
        void RemoveConnector(IElectricWireConnector connector);
        void MarkTopologyDirty();
        void RebuildIfDirty();
        bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment);
        IReadOnlyList<EnergySegment> GetSegments();
    }
}
