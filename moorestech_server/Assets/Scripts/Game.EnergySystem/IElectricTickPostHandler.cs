namespace Game.EnergySystem
{
    /// <summary>
    ///     電力統計の確定後に電力tick後処理（変換機のバッテリー充放電等）を受け取るハンドラ。
    ///     Handler receiving post-electric-tick processing (e.g. converter battery charge/discharge) after the statistics are settled.
    /// </summary>
    public interface IElectricTickPostHandler
    {
        void OnElectricTickPostProcess(ElectricNetworkStatistics statistics);
    }
}
