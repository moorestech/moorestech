namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ベルト搬送tickの唯一の入口。MasterTickUpdaterから全blockの更新の後に呼ばれる
    // 機械からの押し込みはblock更新中に済んでいるので、その後にワールド全体の1組を1tick進める
    // ベルコン個別のUpdateは無く、全segmentの前進はここが担う
    // Sole entry point of the belt transport tick, called by MasterTickUpdater after every block update
    // Machine pushes have landed during block updates, so the single world-wide assembly then advances one tick
    // Belts have no per-block Update; every segment advances here
    public class BeltTransportTickUpdater
    {
        private readonly BeltTransportDatastore _beltTransportDatastore;

        public BeltTransportTickUpdater(BeltTransportDatastore beltTransportDatastore)
        {
            _beltTransportDatastore = beltTransportDatastore;
        }

        public void Update()
        {
            _beltTransportDatastore.Assembly.Simulation.Tick();
        }
    }
}
