using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    /// <summary>
    /// ワイヤーグラフの連結成分としてEnergySegmentを管理するデータストア
    /// Datastore managing EnergySegments as connected components of the wire graph
    /// </summary>
    public interface IElectricWireNetworkDatastore
    {
        int SegmentCount { get; }

        void AddConnector(IElectricWireConnector connector);
        void RemoveConnector(IElectricWireConnector connector);
        void RebuildAround(params IElectricWireConnector[] connectors);
        bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment);
        IReadOnlyList<EnergySegment> GetSegments();
    }
}
