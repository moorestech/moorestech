using System;
using System.Collections.Generic;
using Game.Train.Unit;

namespace Game.Train.Event
{
    // TrainUnit構造更新の通知窓口
    // Notification gateway for train-unit structure updates.
    public interface ITrainUnitSnapshotNotifyEvent
    {
        IObservable<TrainUnitSnapshotNotifyEventData> OnTrainUnitSnapshotNotified { get; }

        void NotifySnapshot(TrainUnit trainUnit);
        void NotifyDeleted(TrainInstanceId trainInstanceId);
        void NotifyChangedByBeforeAfter(IReadOnlyList<TrainUnit> beforeTrains, IReadOnlyList<TrainUnit> afterTrains);
    }
}
