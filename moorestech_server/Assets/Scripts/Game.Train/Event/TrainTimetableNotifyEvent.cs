using System;
using Game.Train.Unit;
using UniRx;

namespace Game.Train.Event
{
    public sealed class TrainTimetableNotifyEvent : ITrainTimetableNotifyEvent
    {
        private readonly Subject<TrainUnit> _subject = new();
        public IObservable<TrainUnit> OnTimetableChanged => _subject;

        public void NotifyTimetableChanged(TrainUnit trainUnit)
        {
            // 未登録の列車は配信先が無いので理由を残して捨てる
            // Drop unregistered trains with a logged reason since no client can address them
            if (trainUnit.TrainUnitInstanceId == TrainUnitInstanceId.Empty)
            {
                UnityEngine.Debug.LogWarning("[TrainTimetableNotify] ignored: train has no instance id");
                return;
            }
            _subject.OnNext(trainUnit);
        }
    }
}
