using Client.Ui.State.BlockInventory.Logic.Detail;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    // 電力網の集約（発電・需要・消費者数・供給率）
    // Electric network aggregate: generation, demand, consumer count and supply rate
    public sealed class ElectricNetworkDetail
    {
        public double TotalGeneratePower { get; }
        public double TotalRequiredPower { get; }
        public int ConsumerCount { get; }
        public double PowerRate { get; }

        public ElectricNetworkDetail(double totalGeneratePower, double totalRequiredPower, int consumerCount, double powerRate)
        {
            TotalGeneratePower = totalGeneratePower;
            TotalRequiredPower = totalRequiredPower;
            ConsumerCount = consumerCount;
            PowerRate = powerRate;
        }
    }

    // 歯車網の集約（供給・要求・停止理由）
    // Gear network aggregate: supply, requirement and stop reason
    public sealed class GearNetworkDetail
    {
        public double TotalGenerateGearPower { get; }
        public double TotalRequiredGearPower { get; }
        public GearNetworkStopReasonKind StopReason { get; }

        public GearNetworkDetail(double totalGenerateGearPower, double totalRequiredGearPower, GearNetworkStopReasonKind stopReason)
        {
            TotalGenerateGearPower = totalGenerateGearPower;
            TotalRequiredGearPower = totalRequiredGearPower;
            StopReason = stopReason;
        }
    }
}
