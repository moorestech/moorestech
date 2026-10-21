using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Context;
using Game.Train.Unit;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.BeltTransportSync
{
    // 搬送tick直後に、そのtickの機械との搬送差分を列車の連番を1つ使って全員へ送る。差分が空でも送り、クライアントが複製を1tick進める合図になる
    // Right after the transport tick, broadcasts that tick's machine handoff diff under one train sequence id; sent even when empty, as the client's cue to advance its replica one tick
    // 組をtick末尾で作り直したら、境界ハッシュより前に作り直し後の全量を次の連番で送る。クライアントは同tickの差分を再生した後にこの全量で複製を組み直す
    // When the assembly is rebuilt at the tick end, the rebuilt full state goes out under the next sequence id before the boundary hash; the client replays that tick's diff, then re-assembles its replica from this full state
    // 全量のハッシュはtickの境界で列車の束が直接計算するので、作り直し後の組と一致する
    // The full-state hash is computed directly by the train bundle at the tick boundary, so it matches the rebuilt assembly
    public sealed class BeltTransportTickEventPacket : IBootInitializable
    {
        public const string TickDiffEventTag = "va:event:beltTransportTickDiff";
        public const string RebuiltFullStateEventTag = "va:event:beltTransportRebuiltFullState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IBeltTransportLookup _beltTransportLookup;
        private readonly BeltTransportTickUpdater _beltTransportTickUpdater;
        private readonly TrainUpdateService _trainUpdateService;

        public BeltTransportTickEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IBeltTransportLookup beltTransportLookup,
            BeltTransportTickUpdater beltTransportTickUpdater,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _beltTransportLookup = beltTransportLookup;
            _beltTransportTickUpdater = beltTransportTickUpdater;
            _trainUpdateService = trainUpdateService;
        }

        public void Load()
        {
            _beltTransportTickUpdater.OnTransportTickCompleted.Subscribe(OnTransportTickCompleted);
            _beltTransportLookup.OnAssemblyRebuilt.Subscribe(_ => OnAssemblyRebuilt());
        }

        // 連番はそれぞれ1つ消費する。割り当てた連番は必ず送る(送らないと欠番になりクライアントが止まる)
        // Each send consumes one sequence id, and an issued id is always sent (an unsent one is a gap that stops the client)
        private void OnTransportTickCompleted(BeltTransportTickReport report)
        {
            var message = new BeltTransportTickDiffMessagePack(_trainUpdateService.GetCurrentTick(), _trainUpdateService.NextTickSequenceId(), report.Diff);
            _eventProtocolProvider.AddBroadcastEvent(TickDiffEventTag, MessagePackSerializer.Serialize(message));
        }

        private void OnAssemblyRebuilt()
        {
            var message = new BeltTransportFullStateMessagePack(_trainUpdateService.GetCurrentTick(), _trainUpdateService.NextTickSequenceId(), _beltTransportLookup.CaptureFullState());
            _eventProtocolProvider.AddBroadcastEvent(RebuiltFullStateEventTag, MessagePackSerializer.Serialize(message));
        }
    }
}
