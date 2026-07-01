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

        public void SetGearState(GearRuntimeState state)
        {
            _gearStates[state.BlockInstanceId] = state;
        }

        public void SetNetworkState(GearNetworkRuntimeState state)
        {
            _networkStates[state.NetworkId] = state;
        }

        public bool TryGetGearState(BlockInstanceId blockInstanceId, out GearRuntimeState state)
        {
            return _gearStates.TryGetValue(blockInstanceId, out state);
        }

        public bool TryGetNetworkState(GearNetworkId networkId, out GearNetworkRuntimeState state)
        {
            return _networkStates.TryGetValue(networkId, out state);
        }

        public void RemoveGear(BlockInstanceId blockInstanceId)
        {
            _gearStates.Remove(blockInstanceId);
        }

        public void RemoveNetwork(GearNetworkId networkId)
        {
            _networkStates.Remove(networkId);
        }

        public static bool TryGetInstance(out GearRuntimeStateStore store)
        {
            store = _instance;
            return store != null;
        }

        public static bool TryGetGearStateStatic(BlockInstanceId blockInstanceId, out GearRuntimeState state)
        {
            if (_instance != null) return _instance.TryGetGearState(blockInstanceId, out state);
            state = default;
            return false;
        }
    }
}
