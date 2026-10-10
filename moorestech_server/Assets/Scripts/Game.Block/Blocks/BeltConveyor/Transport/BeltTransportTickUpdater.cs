using System;
using UniRx;

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
        private readonly Subject<BeltTransportTickReport> _onTransportTickCompleted = new();
        // tick先頭の再構築を覚えておき、搬送後の報告に載せる
        // Remembers a rebuild at the tick head so the post-transport report carries it
        private bool _rebuiltSinceLastTick;

        // 搬送tick直後に1回流れる。tickの束を送る側はこれを購読し、差分か全量を連番つきで送る
        // Fires once right after the transport tick; the tick-bundle sender subscribes and sends the diff or the full state with a sequence id
        public IObservable<BeltTransportTickReport> OnTransportTickCompleted => _onTransportTickCompleted;

        public BeltTransportTickUpdater(BeltTransportDatastore beltTransportDatastore)
        {
            _beltTransportDatastore = beltTransportDatastore;
            _beltTransportDatastore.OnAssemblyRebuilt.Subscribe(_ => _rebuiltSinceLastTick = true);
        }

        public void Update()
        {
            _beltTransportDatastore.Assembly.Simulation.Tick();

            // 差分はこのtick中に成立した分だけ。取り出して報告し、再構築の印は消す
            // The diff holds only what settled during this tick; take it, report it, and clear the rebuild mark
            var report = new BeltTransportTickReport(_rebuiltSinceLastTick, _beltTransportDatastore.DiffRecorder.TakeTickDiff());
            _rebuiltSinceLastTick = false;
            _onTransportTickCompleted.OnNext(report);
        }
    }
}
