using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Context;
using Game.Train.Unit;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.BeltTransportSync
{
    // 接続登録時に、ベルト搬送の全量をその接続へ送る。列車の全量と同じく、連番は消費せず発行済み最新の値をウォーターマークとして載せる(他の接続に欠番を作らない)
    // On connection registration, pushes the belt transport full state to that connection; like the train snapshot it consumes no sequence id and carries the latest issued id as a watermark (no gaps for other connections)
    public sealed class BeltTransportFullSnapshotEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:beltTransportFullSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly BeltTransportDatastore _beltTransportDatastore;
        private readonly TrainUpdateService _trainUpdateService;

        public BeltTransportFullSnapshotEventPacket(
            EventProtocolProvider eventProtocolProvider,
            BeltTransportDatastore beltTransportDatastore,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _beltTransportDatastore = beltTransportDatastore;
            _trainUpdateService = trainUpdateService;
        }

        public void Load()
        {
            _eventProtocolProvider.OnPlayerEventStreamRegistered.Subscribe(PushFullSnapshot);
        }

        private void PushFullSnapshot(int playerId)
        {
            var fullState = BeltTransportFullStateCapture.Capture(_beltTransportDatastore.Assembly);
            var message = new BeltTransportFullStateMessagePack(_trainUpdateService.GetCurrentTick(), _trainUpdateService.GetCurrentTickSequenceId(), fullState);
            _eventProtocolProvider.AddEvent(playerId, EventTag, MessagePackSerializer.Serialize(message));
        }
    }
}
