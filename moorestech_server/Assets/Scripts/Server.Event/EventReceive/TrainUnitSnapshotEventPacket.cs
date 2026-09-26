using Game.Context;
using Game.Train.Event;
using Game.Train.Unit;
using Game.Train.Unit.TickSynchronization;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // Game層のTrainUnit通知をネットワークイベントへ変換する
    // Convert game-layer train notifications into network events.
    public sealed class TrainUnitSnapshotEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:trainUnitSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainTickSequenceSource _trainTickSequenceSource;
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;

        public TrainUnitSnapshotEventPacket(
            EventProtocolProvider eventProtocolProvider,
            TrainTickSequenceSource trainTickSequenceSource,
            ITrainUnitSnapshotNotifyEvent trainUnitSnapshotNotifyEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainTickSequenceSource = trainTickSequenceSource;
            _trainUnitSnapshotNotifyEvent = trainUnitSnapshotNotifyEvent;
        }

        public void Load()
        {
            _trainUnitSnapshotNotifyEvent.OnTrainUnitSnapshotNotified.Subscribe(OnNotified);

            #region Internal

            void OnNotified(TrainUnitSnapshotNotifyEventData notifyEventData)
            {
                if (notifyEventData.TrainUnitInstanceId == TrainUnitInstanceId.Empty)
                {
                    return;
                }

                var payload = CreatePayload(notifyEventData);
                AddBroadcast(payload);
            }

            TrainUnitSnapshotEventMessagePack CreatePayload(TrainUnitSnapshotNotifyEventData notifyEventData)
            {
                var tick = _trainTickSequenceSource.Sequence.Tick;
                var tickSequenceId = _trainTickSequenceSource.Sequence.NextSequenceId();
                if (notifyEventData.IsDeleted)
                {
                    return new TrainUnitSnapshotEventMessagePack(
                        notifyEventData.TrainUnitInstanceId,
                        true,
                        null,
                        tick,
                        tickSequenceId);
                }

                var snapshot = TrainUnitSnapshotFactory.CreateSnapshot(notifyEventData.TrainUnit);
                return new TrainUnitSnapshotEventMessagePack(
                    notifyEventData.TrainUnitInstanceId,
                    false,
                    new TrainUnitSnapshotBundleMessagePack(snapshot),
                    tick,
                    tickSequenceId);
            }

            void AddBroadcast(TrainUnitSnapshotEventMessagePack messagePack)
            {
                var bytes = MessagePackSerializer.Serialize(messagePack);
                _eventProtocolProvider.AddBroadcastEvent(EventTag, bytes);
            }

            #endregion
        }
    }
}
