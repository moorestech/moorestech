using System.Collections.Generic;
using Game.Gear.Common;

namespace Game.Gear.Tick
{
    // gearのtick計算結果のうち、network単位でしか意味を持たない状態を集約する唯一の置き場。
    // gear単位の現在値(RPM/トルク/向き)は保持せず、符号付き原点RPM比×原点RPMから都度導出する。
    // Single home for per-network tick results. Per-gear values (RPM/torque/direction) are not stored; they are derived on demand
    // from the signed origin RPM ratio × the origin RPM.
    public class GearRuntimeStateStore
    {
        private static GearRuntimeStateStore _instance;
        public static GearRuntimeStateStore Instance => _instance;

        private readonly Dictionary<GearNetworkId, GearNetworkRuntimeState> _networkStates = new();

        public GearRuntimeStateStore()
        {
            _instance = this;
        }

        // 未計算のnetworkは空状態（需給0・非停止）として扱う
        // A network never calculated yet is treated as the empty state (zero powers, not stopped)
        public GearNetworkRuntimeState GetNetworkState(GearNetworkId networkId)
        {
            return _networkStates.TryGetValue(networkId, out var state) ? state : default;
        }

        public void SetNetworkState(GearNetworkId networkId, GearNetworkRuntimeState state)
        {
            _networkStates[networkId] = state;
        }

        public void RemoveNetworkState(GearNetworkId networkId)
        {
            _networkStates.Remove(networkId);
        }
    }
}
