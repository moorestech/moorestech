using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // 確定済み電力網だけを読む契約
    // Read-only contract for the applied electric network
    public interface IElectricWireNetworkLookup
    {
        bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment);
        IReadOnlyList<EnergySegment> GetSegments();
    }
}
