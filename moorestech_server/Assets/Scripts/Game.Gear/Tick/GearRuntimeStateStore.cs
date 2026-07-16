using System.Collections.Generic;
using Game.Gear.Common;

namespace Game.Gear.Tick
{
    // gear網単位のtick結果を保持する
    // Holds tick calculation results for each applied gear network
    internal class GearRuntimeStateStore
    {
        private readonly Dictionary<GearNetworkId, GearNetworkRuntimeState> _networkStates = new();

        internal static GearRuntimeStateStore Instance { get; private set; }

        internal static void Activate(GearRuntimeStateStore runtimeStateStore)
        {
            Instance = runtimeStateStore;
        }

        internal GearNetworkRuntimeState GetNetworkState(GearNetworkId networkId)
        {
            return _networkStates.TryGetValue(networkId, out var state) ? state : default;
        }

        internal void SetNetworkState(GearNetworkId networkId, GearNetworkRuntimeState state)
        {
            _networkStates[networkId] = state;
        }

        internal void Destroy()
        {
            _networkStates.Clear();
        }
    }
}
