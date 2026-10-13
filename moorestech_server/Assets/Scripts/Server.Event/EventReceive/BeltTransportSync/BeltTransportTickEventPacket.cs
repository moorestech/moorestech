using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Context;
using Game.Train.Unit;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.BeltTransportSync
{
    // 搬送tick直後に、そのtickの機械との搬送差分を列車の連番を1つ使って全員へ送る。差分が空でも送り、クライアントが複製を1tick進める合図になる
    // Right after the transport tick, broadcasts that tick's machine handoff diff under one train sequence id; sent even when empty, as the client's cue to advance its replica one tick
    // 組を作り直したtickは差分の代わりに搬送後の全量を送る。クライアントは複製を全量から組み直し、そのtickの差分は再生しない
    // A rebuilt tick sends the post-tick full state instead of the diff; the client re-assembles its replica from it and replays no diff for that tick
    // 全量のハッシュはtickの境界で列車の束が直接計算する(次の組み直しはその後の次tick先頭なので搬送直後と同じ値になる)
    // The full-state hash is computed directly by the train bundle at the tick boundary (the next rebuild comes later, at the next tick head, so it equals the post-transport value)
    public sealed class BeltTransportTickEventPacket : IBootInitializable
    {
        public const string TickDiffEventTag = "va:event:beltTransportTickDiff";
        public const string RebuiltFullStateEventTag = "va:event:beltTransportRebuiltFullState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly BeltTransportDatastore _beltTransportDatastore;
        private readonly BeltTransportTickUpdater _beltTransportTickUpdater;
        private readonly TrainUpdateService _trainUpdateService;

        public BeltTransportTickEventPacket(
            EventProtocolProvider eventProtocolProvider,
            BeltTransportDatastore beltTransportDatastore,
            BeltTransportTickUpdater beltTransportTickUpdater,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _beltTransportDatastore = beltTransportDatastore;
            _beltTransportTickUpdater = beltTransportTickUpdater;
            _trainUpdateService = trainUpdateService;
        }

        public void Load()
        {
            _beltTransportTickUpdater.OnTransportTickCompleted.Subscribe(OnTransportTickCompleted);
        }

        private void OnTransportTickCompleted(BeltTransportTickReport report)
        {
            // 連番はここで1つ消費する。割り当てた連番は必ず送る(送らないと欠番になりクライアントが止まる)
            // One sequence id is consumed here, and an issued id is always sent (an unsent one is a gap that stops the client)
            var tick = _trainUpdateService.GetCurrentTick();
            var tickSequenceId = _trainUpdateService.NextTickSequenceId();
            if (report.WasRebuilt) SendRebuiltFullState();
            else SendTickDiff(report.Diff);

            #region Internal

            void SendRebuiltFullState()
            {
                var fullState = BeltTransportFullStateCapture.Capture(_beltTransportDatastore.Assembly);
                var message = new BeltTransportFullStateMessagePack(tick, tickSequenceId, fullState);
                _eventProtocolProvider.AddBroadcastEvent(RebuiltFullStateEventTag, MessagePackSerializer.Serialize(message));
            }

            void SendTickDiff(BeltTickDiff diff)
            {
                var message = new BeltTransportTickDiffMessagePack(tick, tickSequenceId, diff);
                _eventProtocolProvider.AddBroadcastEvent(TickDiffEventTag, MessagePackSerializer.Serialize(message));
            }

            #endregion
        }
    }
}
