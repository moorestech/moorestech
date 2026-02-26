using System;
using Game.Train.Unit;
using UniRx;
using UnityEngine;

namespace Game.Train.Event
{
    // TrainUnit構造変更の通知を集約する
    // Aggregate notifications for train-unit structure changes.
    public sealed class TrainUnitSnapshotNotifyEvent : ITrainUnitSnapshotNotifyEvent
    {
        private readonly Subject<TrainUnitSnapshotNotifyEventData> _subject = new();
        public IObservable<TrainUnitSnapshotNotifyEventData> OnTrainUnitSnapshotNotified => _subject;
        
        public void NotifySnapshot(TrainUnit trainUnit)
        {
            if (trainUnit == null || trainUnit.TrainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }
            trainUnit.ResetDiff();
            Debug.Log("aaaa");
            // 単機スナップショットの更新を通知する
            // Notify that a single train unit snapshot should be updated.
            _subject.OnNext(new TrainUnitSnapshotNotifyEventData(trainUnit.TrainInstanceId, false, trainUnit));
        }

        public void NotifyDeleted(TrainInstanceId trainInstanceId)
        {
            if (trainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }
            Debug.Log("bbb");

            // 編成削除通知を発行する
            // Notify that a train unit has been deleted.
            _subject.OnNext(new TrainUnitSnapshotNotifyEventData(trainInstanceId, true, null));
        }
    }
}
