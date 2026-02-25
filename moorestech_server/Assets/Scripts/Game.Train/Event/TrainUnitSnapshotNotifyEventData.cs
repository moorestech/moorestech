using Game.Train.Unit;

namespace Game.Train.Event
{
    // TrainUnit構造更新通知のデータ
    // Data model for train-unit structure update notifications.
    public readonly struct TrainUnitSnapshotNotifyEventData
    {
        public TrainInstanceId TrainInstanceId { get; }
        public bool IsDeleted { get; }
        public TrainUnit TrainUnit { get; }

        public TrainUnitSnapshotNotifyEventData(TrainInstanceId trainInstanceId, bool isDeleted, TrainUnit trainUnit)
        {
            TrainInstanceId = trainInstanceId;
            IsDeleted = isDeleted;
            TrainUnit = trainUnit;
        }
    }
}
