namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 発電機の詳細の写し（残り・現在の燃料の秒と稼働率）
    /// Snapshot of a generator's detail (remaining and current fuel seconds, operating rate)
    /// </summary>
    public sealed class GeneratorDetail
    {
        public double RemainingFuelTime { get; }
        public double CurrentFuelTime { get; }
        public double OperatingRate { get; }

        public GeneratorDetail(double remainingFuelTime, double currentFuelTime, double operatingRate)
        {
            RemainingFuelTime = remainingFuelTime;
            CurrentFuelTime = currentFuelTime;
            OperatingRate = operatingRate;
        }
    }
}
