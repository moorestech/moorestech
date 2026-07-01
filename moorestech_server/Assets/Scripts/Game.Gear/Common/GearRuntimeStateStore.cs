using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public class GearRuntimeStateStore
    {
        private static GearRuntimeStateStore _instance;
        private readonly Dictionary<BlockInstanceId, GearRuntimeState> _gearStates = new();
        private readonly Dictionary<GearNetworkId, GearNetworkRuntimeState> _networkStates = new();

        public GearRuntimeStateStore()
        {
            _instance = this;
        }

        public static GearRuntimeStateStore GetOrCreateForManualUpdate()
        {
            return _instance ?? new GearRuntimeStateStore();
        }

        public static bool TryGetGearState(BlockInstanceId blockInstanceId, out GearRuntimeState state)
        {
            if (_instance == null)
            {
                state = default;
                return false;
            }

            return _instance._gearStates.TryGetValue(blockInstanceId, out state);
        }

        public static void SetGearStateFromTransformer(BlockInstanceId blockInstanceId, RPM rpm, Torque torque, bool isClockwise, bool isStopped, GearNetworkStopReason stopReason)
        {
            if (_instance == null) return;

            var networkId = new GearNetworkId(0);
            if (_instance._gearStates.TryGetValue(blockInstanceId, out var currentState))
            {
                networkId = currentState.NetworkId;
                if (isStopped && currentState.IsStopped) stopReason = currentState.StopReason;
            }

            var state = new GearRuntimeState(blockInstanceId, networkId, rpm, torque, isClockwise, isStopped, stopReason);
            _instance._gearStates[blockInstanceId] = state;
        }

        public void Clear()
        {
            _gearStates.Clear();
            _networkStates.Clear();
        }

        public void SetNetworkState(GearNetworkRuntimeState state)
        {
            _networkStates[state.NetworkId] = state;
        }

        public void SetGearState(GearRuntimeState state)
        {
            _gearStates[state.BlockInstanceId] = state;
        }
    }
}
