using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // live電線と適用済みsegmentを分離する。dirty再構築はtick先頭で具象datastore経由（MasterTickUpdater）
    // Separates live wire registration from applied segments; the dirty rebuild runs at tick head via the concrete datastore (MasterTickUpdater)
    public interface IElectricWireNetworkDatastore
    {
        void AddConnector(IElectricWireConnector connector);
        void RemoveConnector(IElectricWireConnector connector);
        void MarkTopologyDirty();
        bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment);
        IReadOnlyList<EnergySegment> GetSegments();
    }
}
