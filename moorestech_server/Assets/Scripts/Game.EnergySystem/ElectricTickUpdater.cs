namespace Game.EnergySystem
{
    /// <summary>
    ///     電力tickの唯一の入口。GameUpdater.AdditionalUpdatesに登録され、GearTickUpdaterより前に走る。
    ///     全セグメントを毎tick走査し、需要・発電量を再集計→供給率確定→変換機等の後処理、の順で処理する。
    ///     Sole entry point of the electric tick, registered in GameUpdater.AdditionalUpdates to run before GearTickUpdater.
    ///     Every tick it scans all segments: re-aggregate demand and generation, settle the supply rate, then run converter post-processing.
    /// </summary>
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
            // 保留中のワイヤートポロジ変更をFIFOで一括適用し、tick内のセグメント構成を確定する
            // Apply pending wire topology commands FIFO, fixing the segment composition for the rest of the tick
            _electricWireNetworkDatastore.FlushPendingCommands();

            // 全セグメントの需給を毎tick再集計し、統計確定→電力tick後処理を実行する
            // Re-aggregate every segment's supply and demand each tick, settle statistics, then run post-tick processing
            foreach (var segment in _electricWireNetworkDatastore.GetSegments())
            {
                segment.SettleTick();
                segment.RunPostTickProcess();
            }
        }
    }
}
