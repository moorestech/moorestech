using Game.Train.Unit;

namespace Game.Train.Event
{
    // TrainUnit構造更新通知のデータ
    // Data model for train-unit structure update notifications.
    public readonly struct TrainUnitSnapshotNotifyEventData
    {
        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public bool IsDeleted { get; }
        public TrainUnit TrainUnit { get; }

        public TrainUnitSnapshotNotifyEventData(TrainUnitInstanceId trainUnitInstanceId, bool isDeleted, TrainUnit trainUnit)
        {
            TrainUnitInstanceId = trainUnitInstanceId;
            IsDeleted = isDeleted;
            TrainUnit = trainUnit;
        }
    }
}
