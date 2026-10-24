using Game.Block.Blocks.Fluid;
using Game.Context;
using Game.Fluid.Sync.Message;
using Game.Train.Unit;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.FluidNetworkSync
{
    // 接続登録時に、流体網の全量をその接続へ送る。列車・ベルトの全量と同じく、連番は消費せず発行済み最新の値をウォーターマークとして載せる(他の接続に欠番を作らない)
    // On connection registration, pushes the fluid network full state to that connection; like the train and belt snapshots it consumes no sequence id and carries the latest issued id as a watermark (no gaps for other connections)
    public sealed class FluidNetworkFullSnapshotEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:fluidNetworkFullSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IFluidNetworkLookup _fluidNetworkLookup;
        private readonly TrainUpdateService _trainUpdateService;

        public FluidNetworkFullSnapshotEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IFluidNetworkLookup fluidNetworkLookup,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _fluidNetworkLookup = fluidNetworkLookup;
            _trainUpdateService = trainUpdateService;
        }

        public void Load()
        {
            _eventProtocolProvider.OnPlayerEventStreamRegistered.Subscribe(PushFullSnapshot);
        }

        private void PushFullSnapshot(int playerId)
        {
            var message = new FluidNetworkFullStateMessagePack(_trainUpdateService.GetCurrentTick(), _trainUpdateService.GetCurrentTickSequenceId(), _fluidNetworkLookup.CaptureFullState());
            _eventProtocolProvider.AddEvent(playerId, EventTag, MessagePackSerializer.Serialize(message));
        }
    }
}
