namespace Game.EnergySystem
{
    // 適用済み全電力セグメントの需給確定と後処理だけを実行する
    // Settles supply and demand and runs post-processing for every applied electric segment
    public class ElectricTickUpdater
    {
        // gear側のGearTickUpdater同様、internalなflushを呼ぶため具象datastoreを受け取る
        // Like GearTickUpdater on the gear side, this takes the concrete datastore to call its internal flush
        private readonly ElectricWireNetworkDatastore _electricWireNetworkDatastore;

        public ElectricTickUpdater(ElectricWireNetworkDatastore electricWireNetworkDatastore)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
        }

        public void Update()
        {
            // 再構築後は需給計算だけ行う
            // A preceding delegate owns topology rebuilding; this updater performs settlement only
            foreach (var segment in _electricWireNetworkDatastore.GetSegments())
            {
                segment.SettleTick();
                segment.RunPostTickProcess();
            }
        }
    }
}
