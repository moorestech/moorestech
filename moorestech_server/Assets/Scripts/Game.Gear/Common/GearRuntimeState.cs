using Game.Block.Interface;

namespace Game.Gear.Common
{
    public readonly struct GearRuntimeState
    {
        public readonly BlockInstanceId BlockInstanceId;
        public readonly GearNetworkId NetworkId;
        public readonly RPM Rpm;
        public readonly Torque Torque;
        public readonly bool IsClockwise;
        public readonly bool IsStopped;
        public readonly GearNetworkStopReason StopReason;
        public bool HasSupply => !IsStopped && Rpm.AsPrimitive() > 0f && Torque.AsPrimitive() > 0f;

        public GearRuntimeState(BlockInstanceId blockInstanceId, GearNetworkId networkId, RPM rpm, Torque torque, bool isClockwise, bool isStopped, GearNetworkStopReason stopReason)
        {
            BlockInstanceId = blockInstanceId;
            NetworkId = networkId;
            Rpm = rpm;
            Torque = torque;
            IsClockwise = isClockwise;
            IsStopped = isStopped;
            StopReason = stopReason;
        }
    }

    public readonly struct GearNetworkRuntimeState
    {
        public readonly GearNetworkId NetworkId;
        public readonly float DemandPower;
        public readonly float AvailablePower;
        public readonly float NetworkLoadRate;
        public readonly bool IsStopped;
        public readonly GearNetworkStopReason StopReason;

        public GearNetworkRuntimeState(GearNetworkId networkId, float demandPower, float availablePower, float networkLoadRate, bool isStopped, GearNetworkStopReason stopReason)
        {
            NetworkId = networkId;
            DemandPower = demandPower;
            AvailablePower = availablePower;
            NetworkLoadRate = networkLoadRate;
            IsStopped = isStopped;
            StopReason = stopReason;
        }
    }
}
