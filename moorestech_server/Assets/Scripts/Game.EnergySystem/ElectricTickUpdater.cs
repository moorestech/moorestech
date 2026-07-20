namespace Game.EnergySystem
{
    // 適用済み全電力セグメントの需給確定と後処理だけを実行する
    // Settles supply and demand and runs post-processing for every applied electric segment
    public class ElectricTickUpdater
    {
        private readonly IElectricWireNetworkDatastore _electricWireNetworkDatastore;

        public ElectricTickUpdater(IElectricWireNetworkDatastore electricWireNetworkDatastore)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
        }

        public void Update()
        {
            // 再構築はtick先頭の先行登録（RebuildIfDirty）が担い、ここでは需給計算だけ行う
            // A preceding tick-head registration (RebuildIfDirty) owns the rebuild; this updater performs settlement only
            foreach (var segment in _electricWireNetworkDatastore.GetSegments())
            {
                segment.SettleTick();
                segment.RunPostTickProcess();
            }
        }
    }
}
