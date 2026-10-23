using Game.Block.Blocks.Fluid;
using Game.Context;
using Game.Fluid.Sync.Diff;
using Game.Fluid.Sync.Message;
using Game.Train.Unit;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.FluidNetworkSync
{
    // ブロック更新の後に、そのtickの境界の出入りを列車の連番を1つ使って全員へ送る。差分が空でも送り、クライアントが複製を1tick進める合図になる
    // After block updates, broadcasts that tick's boundary exchange under one train sequence id; sent even when empty, as the client's cue to advance its replica one tick
    // 網をtick末尾で作り直したら、境界ハッシュより前に作り直し後の全量を次の連番で送る。クライアントは同tickの差分を再生した後にこの全量で複製を組み直す
    // When the network is rebuilt at the tick end, the rebuilt full state goes out under the next sequence id before the boundary hash; the client replays that tick's diff, then re-assembles its replica from this full state
    public sealed class FluidNetworkTickEventPacket : IBootInitializable
    {
        public const string TickDiffEventTag = "va:event:fluidNetworkTickDiff";
        public const string RebuiltFullStateEventTag = "va:event:fluidNetworkRebuiltFullState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IFluidNetworkLookup _fluidNetworkLookup;
        private readonly FluidTickUpdater _fluidTickUpdater;
        private readonly TrainUpdateService _trainUpdateService;

        public FluidNetworkTickEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IFluidNetworkLookup fluidNetworkLookup,
            FluidTickUpdater fluidTickUpdater,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _fluidNetworkLookup = fluidNetworkLookup;
            _fluidTickUpdater = fluidTickUpdater;
            _trainUpdateService = trainUpdateService;
        }

        public void Load()
        {
            _fluidTickUpdater.OnTickDiffPublished.Subscribe(OnTickDiffPublished);
            _fluidNetworkLookup.OnTopologyRebuilt.Subscribe(_ => OnTopologyRebuilt());

            #region Internal

            void OnTopologyRebuilt()
            {
                var message = new FluidNetworkFullStateMessagePack(_trainUpdateService.GetCurrentTick(), _trainUpdateService.NextTickSequenceId(), _fluidNetworkLookup.CaptureFullState());
                _eventProtocolProvider.AddBroadcastEvent(RebuiltFullStateEventTag, MessagePackSerializer.Serialize(message));
            }

            #endregion
        }

        // 連番はそれぞれ1つ消費する。割り当てた連番は必ず送る(送らないと欠番になりクライアントが止まる)
        // Each send consumes one sequence id, and an issued id is always sent (an unsent one is a gap that stops the client)
        private void OnTickDiffPublished(FluidTickDiff diff)
        {
            var message = new FluidNetworkTickDiffMessagePack(_trainUpdateService.GetCurrentTick(), _trainUpdateService.NextTickSequenceId(), diff);
            _eventProtocolProvider.AddBroadcastEvent(TickDiffEventTag, MessagePackSerializer.Serialize(message));
        }
    }
}
