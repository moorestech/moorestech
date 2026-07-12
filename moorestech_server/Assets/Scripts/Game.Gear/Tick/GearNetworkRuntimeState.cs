using Game.Gear.Common;

namespace Game.Gear.Tick
{
    // network 1つ分のそのtickの確定状態。gear個別には複製せずnetwork単位で1つだけ持つ
    // Finalized per-network state for the tick; held once per network instead of duplicated per gear
    public readonly struct GearNetworkRuntimeState
    {
        public readonly bool IsStopped;
        public readonly GearNetworkStopReason StopReason;

        // Σ(rpm × requiredTorque)。networkの要求動力
        // Σ(rpm × requiredTorque); total power demanded by the network
        public readonly float DemandPower;

        // Σ(generateRpm × generateTorque)。networkの供給可能動力
        // Σ(generateRpm × generateTorque); total power the network can supply
        public readonly float AvailablePower;

        // demandPower / availablePower を1以下にクランプした負荷率。停止時は0
        // Load rate = demand / available clamped to 1; zero while stopped
        public readonly float NetworkLoadRate;

        public GearNetworkRuntimeState(bool isStopped, GearNetworkStopReason stopReason, float demandPower, float availablePower, float networkLoadRate)
        {
            IsStopped = isStopped;
            StopReason = stopReason;
            DemandPower = demandPower;
            AvailablePower = availablePower;
            NetworkLoadRate = networkLoadRate;
        }
    }
}
