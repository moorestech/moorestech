using System;
using Game.Train.Unit;

namespace Game.Train.Event
{
    // 時刻表・自動運転状態の変化をUI向けに知らせる窓口（tick同期しない）
    // Gateway notifying timetable and auto-run changes to the UI (not tick-synchronized)
    public interface ITrainTimetableNotifyEvent
    {
        IObservable<TrainUnit> OnTimetableChanged { get; }
        void NotifyTimetableChanged(TrainUnit trainUnit);
    }
}
