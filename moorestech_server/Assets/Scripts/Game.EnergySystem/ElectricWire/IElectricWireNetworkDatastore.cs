using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    /// <summary>
    /// ワイヤーグラフの連結成分としてEnergySegmentを管理するデータストア。
    /// トポロジ変更はコマンドとして保留され、電力tick先頭に具象側のflushで一括反映される。
    /// Datastore managing EnergySegments as connected components of the wire graph.
    /// Topology changes are queued as commands and applied in batch by the concrete datastore's flush at the head of the electric tick.
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
