using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     TrainUnit生成イベントをブロードキャストするパケット
    ///     Event packet that broadcasts newly created train units
    /// </summary>
    public sealed class TrainUnitCreatedEventPacket
    {
        public const string EventTag = "va:event:trainUnitCreated";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainUnitCreatedEventPacket(EventProtocolProvider eventProtocolProvider, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            // 列車生成イベントを購読する
            // Subscribe to train unit creation events
            _trainUpdateService.TrainUnitCreatedEvent.Subscribe(OnTrainUnitCreated);
        }

        private void OnTrainUnitCreated(TrainUnitInitializationNotifier.TrainUnitCreatedData data)
        {
            // 列車生成差分をブロードキャストする
            // Broadcast the train unit creation diff
            var snapshot = TrainUnitSnapshotFactory.CreateSnapshot(data.TrainUnit);
            var snapshotPack = new TrainUnitSnapshotBundleMessagePack(snapshot);
            var tickSequenceId = _trainUpdateService.NextTickSequenceId();
            var message = new TrainUnitCreatedEventMessagePack(snapshotPack, _trainUpdateService.GetCurrentTick(), tickSequenceId);
            var payload = MessagePackSerializer.Serialize(message);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}
