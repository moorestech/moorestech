namespace Game.EnergySystem
{
    // 適用済み全電力セグメントの需給確定と後処理だけを実行する
    // Settles supply and demand and runs post-processing for every applied electric segment
    public class ElectricTickUpdater
    {
        private readonly IElectricWireNetworkLookup _electricWireNetworkLookup;
        private readonly IElectricWireNetworkMutation _electricWireNetworkMutation;

        public ElectricTickUpdater(
            IElectricWireNetworkLookup electricWireNetworkLookup,
            IElectricWireNetworkMutation electricWireNetworkMutation)
        {
            _electricWireNetworkLookup = electricWireNetworkLookup;
            _electricWireNetworkMutation = electricWireNetworkMutation;
        }

        public void Update()
        {
            // 再構築後は需給計算だけ行う
            // A preceding delegate owns topology rebuilding; this updater performs settlement only
            foreach (var segment in _electricWireNetworkLookup.GetSegments())
            {
                segment.SettleTick();
                segment.RunPostTickProcess();
            }
            _electricWireNetworkMutation.MarkStatisticsSettled();
        }
    }
}
