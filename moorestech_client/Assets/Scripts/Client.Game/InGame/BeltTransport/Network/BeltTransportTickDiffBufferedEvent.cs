using Client.Game.InGame.Train.Network;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;

namespace Client.Game.InGame.BeltTransport.Network
{
    // 列車の順序待ちキューに載る、1tick分のベルト差分。流された時点で複製をそのtickぶん進める
    // One tick's belt diff on the train's ordered buffer; advances the replica by that tick when flushed
    public sealed class BeltTransportTickDiffBufferedEvent : ITrainTickBufferedEvent
    {
        private readonly BeltTransportClientCache _cache;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly BeltTickDiff _diff;

        public BeltTransportTickDiffBufferedEvent(BeltTransportClientCache cache, TrainUnitFutureMessageBuffer futureMessageBuffer, BeltTickDiff diff)
        {
            _cache = cache;
            _futureMessageBuffer = futureMessageBuffer;
            _diff = diff;
        }

        public void Apply()
        {
            // 載らない搬入・消費されない搬出は複製の食い違い。再同期はせず、列車と同じ恒久停止に入る(理由は複製がログ済み)
            // An insert that does not fit or an unconsumed extract is a replica divergence; no resync, the same permanent stop as the train (the replica logged the reason)
            if (_cache.ApplyTickDiff(_diff)) return;
            _futureMessageBuffer.StopRetainingFutureMessages("BeltTransportReplicaDiverged");
        }
    }
}
