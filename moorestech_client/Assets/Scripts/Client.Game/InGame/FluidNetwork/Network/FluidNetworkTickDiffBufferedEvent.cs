using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Game.Fluid.Sync.Diff;

namespace Client.Game.InGame.FluidNetwork.Network
{
    // 列車の順序待ちキューに載る、1tick分の流体差分。流された時点で複製をそのtickぶん進める
    // One tick's fluid diff on the train's ordered buffer; advances the replica by that tick when flushed
    public sealed class FluidNetworkTickDiffBufferedEvent : ITrainTickBufferedEvent
    {
        private readonly FluidNetworkClientCache _cache;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainSynchronizationDiagnostics _diagnostics;
        private readonly ulong _tickUnifiedId;
        private readonly FluidTickDiff _diff;

        public FluidNetworkTickDiffBufferedEvent(FluidNetworkClientCache cache, TrainUnitFutureMessageBuffer futureMessageBuffer, TrainSynchronizationDiagnostics diagnostics, ulong tickUnifiedId, FluidTickDiff diff)
        {
            _cache = cache;
            _futureMessageBuffer = futureMessageBuffer;
            _diagnostics = diagnostics;
            _tickUnifiedId = tickUnifiedId;
            _diff = diff;
        }

        public void Apply()
        {
            // 再現できない出入りは複製の食い違い。再同期はせず、列車と同じ恒久停止に入る(理由は複製がログ済み)
            // An exchange the replica cannot reproduce is a divergence; no resync, the same permanent stop as the train (the replica logged the reason)
            if (_cache.ApplyTickDiff(_diff)) return;
            _diagnostics.RecordReplicaDivergence(_tickUnifiedId, "FluidNetwork");
            _futureMessageBuffer.StopRetainingFutureMessages("FluidNetworkReplicaDiverged");
        }
    }
}
