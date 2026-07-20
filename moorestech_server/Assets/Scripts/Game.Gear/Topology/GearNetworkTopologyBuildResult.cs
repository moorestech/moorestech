using System.Collections.Generic;
using Game.Gear.Common;

namespace Game.Gear.Topology
{
    // topologyと全runtime派生集合を交換前に一体として完成する
    // Completes topology and every derived runtime collection together before swapping
    internal class GearNetworkTopologyBuildResult
    {
        public readonly GearNetworkTopologyMap TopologyMap;
        public readonly HashSet<GearNetwork> NetworksRequiringRecalc;
        public readonly HashSet<GearNetwork> ContinuousTickNetworks;

        public GearNetworkTopologyBuildResult(
            GearNetworkTopologyMap topologyMap,
            HashSet<GearNetwork> networksRequiringRecalc,
            HashSet<GearNetwork> continuousTickNetworks)
        {
            TopologyMap = topologyMap;
            NetworksRequiringRecalc = networksRequiringRecalc;
            ContinuousTickNetworks = continuousTickNetworks;
        }
    }
}
