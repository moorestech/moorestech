using System;
using Game.Train.Unit;
using UniRx;

namespace Game.Train.Event
{
    // TrainUnit構造変更の通知を集約する
    // Aggregate notifications for train-unit structure changes.
    public sealed class TrainUnitSnapshotNotifyEvent : ITrainUnitSnapshotNotifyEvent
    {
        private readonly Subject<TrainUnitSnapshotNotifyEventData> _subject = new();
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        public IObservable<TrainUnitSnapshotNotifyEventData> OnTrainUnitSnapshotNotified => _subject;

        public TrainUnitSnapshotNotifyEvent(ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
        }
        
        public void NotifySnapshot(TrainUnit trainUnit)
        {
            if (trainUnit == null || trainUnit.TrainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }
            trainUnit.ResetDiff();
            // 単機スナップショットの更新を通知する
            // Notify that a single train unit snapshot should be updated.
            _subject.OnNext(new TrainUnitSnapshotNotifyEventData(trainUnit.TrainInstanceId, false, trainUnit));
        }

        public void NotifySnapshotByCar(TrainCar trainCar)
        {
            // 車両から所属TrainUnitを解決し、既存の単機snapshot通知経路に流す。
            // Resolve the owning TrainUnit from the car and reuse the existing per-unit snapshot path.
            if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(trainCar.TrainCarInstanceId, out var trainUnit)) return;

            NotifySnapshot(trainUnit);
        }

        public void NotifyDeleted(TrainInstanceId trainInstanceId)
        {
            if (trainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 編成削除通知を発行する
            // Notify that a train unit has been deleted.
            _subject.OnNext(new TrainUnitSnapshotNotifyEventData(trainInstanceId, true, null));
        }
    }
}
