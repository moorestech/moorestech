using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    // TrainUnit単位の構造変化をスナップショットで通知する
    // Broadcast per-train-unit structure updates as snapshots.
    public sealed class TrainUnitSnapshotEventPacket
    {
        public const string EventTag = "va:event:trainUnitSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainUnitSnapshotEventPacket(EventProtocolProvider eventProtocolProvider, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
        }

        public void BroadcastSnapshot(TrainUnit trainUnit)
        {
            if (trainUnit == null || trainUnit.TrainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 現在状態を単機スナップショットへ変換して配信する
            // Convert current state into a per-unit snapshot and broadcast it.
            var snapshot = TrainUnitSnapshotFactory.CreateSnapshot(trainUnit);
            var payload = new TrainUnitSnapshotEventMessagePack(
                trainUnit.TrainInstanceId,
                false,
                new TrainUnitSnapshotBundleMessagePack(snapshot),
                _trainUpdateService.GetCurrentTick(),
                _trainUpdateService.NextTickSequenceId());
            AddBroadcast(payload);
        }

        public void BroadcastDeleted(TrainInstanceId trainInstanceId)
        {
            if (trainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 消滅した編成は tombstone として通知する
            // Broadcast a tombstone for removed train units.
            var payload = new TrainUnitSnapshotEventMessagePack(
                trainInstanceId,
                true,
                null,
                _trainUpdateService.GetCurrentTick(),
                _trainUpdateService.NextTickSequenceId());
            AddBroadcast(payload);
        }

        #region Internal

        private void AddBroadcast(TrainUnitSnapshotEventMessagePack payload)
        {
            var bytes = MessagePackSerializer.Serialize(payload);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, bytes);
        }

        #endregion
    }
}
