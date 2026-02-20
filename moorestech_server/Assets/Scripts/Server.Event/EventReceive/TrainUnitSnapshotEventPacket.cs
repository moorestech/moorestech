using Game.Train.Event;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // Game層のTrainUnit通知をネットワークイベントへ変換する
    // Convert game-layer train notifications into network events.
    public sealed class TrainUnitSnapshotEventPacket
    {
        public const string EventTag = "va:event:trainUnitSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainUnitSnapshotEventPacket(
            EventProtocolProvider eventProtocolProvider,
            TrainUpdateService trainUpdateService,
            ITrainUnitSnapshotNotifyEvent trainUnitSnapshotNotifyEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            trainUnitSnapshotNotifyEvent.OnTrainUnitSnapshotNotified.Subscribe(OnNotified);
        }

        #region Internal

        private void OnNotified(TrainUnitSnapshotNotifyEventData notifyEventData)
        {
            if (notifyEventData.TrainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 通知内容をイベントペイロードに変換して配信する
            // Convert notification data into event payload and broadcast it.
            var payload = CreatePayload(notifyEventData);
            AddBroadcast(payload);
        }

        private TrainUnitSnapshotEventMessagePack CreatePayload(TrainUnitSnapshotNotifyEventData notifyEventData)
        {
            var tick = _trainUpdateService.GetCurrentTick();
            var tickSequenceId = _trainUpdateService.NextTickSequenceId();
            if (notifyEventData.IsDeleted)
            {
                return new TrainUnitSnapshotEventMessagePack(
                    notifyEventData.TrainInstanceId,
                    true,
                    null,
                    tick,
                    tickSequenceId);
            }

            var snapshot = TrainUnitSnapshotFactory.CreateSnapshot(notifyEventData.TrainUnit);
            return new TrainUnitSnapshotEventMessagePack(
                notifyEventData.TrainInstanceId,
                false,
                new TrainUnitSnapshotBundleMessagePack(snapshot),
                tick,
                tickSequenceId);
        }

        private void AddBroadcast(TrainUnitSnapshotEventMessagePack messagePack)
        {
            var bytes = MessagePackSerializer.Serialize(messagePack);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, bytes);
        }

        #endregion
    }
}
