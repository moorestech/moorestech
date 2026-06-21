namespace Game.EnergySystem
{
    /// <summary>
    ///     電力ネットワークの集約統計。供給率は1.0クランプ済みで、ConsumerCountが0なら需要なしを表す
    ///     Aggregated statistics of an energy network. PowerRate is clamped to 1.0, and ConsumerCount == 0 means no demand
    /// </summary>
    public readonly struct ElectricNetworkStatistics
    {
        public readonly float TotalGeneratePower;
        public readonly float TotalRequiredPower;
        public readonly float PowerRate;
        public readonly int ConsumerCount;

        public ElectricNetworkStatistics(float totalGeneratePower, float totalRequiredPower, float powerRate, int consumerCount)
        {
            TotalGeneratePower = totalGeneratePower;
            TotalRequiredPower = totalRequiredPower;
            PowerRate = powerRate;
            ConsumerCount = consumerCount;
        }
    }
}
