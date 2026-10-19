using System;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ベルト搬送tickの唯一の入口。MasterTickUpdaterから全blockの更新の後に呼ばれる
    // 機械からの押し込みはblock更新中に済んでいるので、その後にワールド全体の1組を1tick進めて報告を流す
    // ベルコン個別のUpdateは無く、全segmentの前進はここが担う
    // Sole entry point of the belt transport tick, called by MasterTickUpdater after every block update
    // Machine pushes have landed during block updates, so the single world-wide assembly then advances one tick and its report is published
    // Belts have no per-block Update; every segment advances here
    public class BeltTransportTickUpdater
    {
        private readonly IBeltTransportMutation _beltTransportMutation;
        private readonly Subject<BeltTransportTickReport> _onTransportTickCompleted = new();

        // 搬送tick直後に1回流れる。tickの束を送る側はこれを購読し、差分か全量を連番つきで送る
        // Fires once right after the transport tick; the tick-bundle sender subscribes and sends the diff or the full state with a sequence id
        public IObservable<BeltTransportTickReport> OnTransportTickCompleted => _onTransportTickCompleted;

        public BeltTransportTickUpdater(IBeltTransportMutation beltTransportMutation)
        {
            _beltTransportMutation = beltTransportMutation;
        }

        public void Update()
        {
            _onTransportTickCompleted.OnNext(_beltTransportMutation.Tick());
        }
    }
}
