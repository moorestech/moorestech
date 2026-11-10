using UniRx;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いている列車の時刻表の状態。列車が開いていなければ Unavailable
    /// Timetable state of the open train; unavailable when no train is open
    /// </summary>
    public interface ITrainTimetableSource
    {
        IReadOnlyReactiveProperty<TrainTimetableViewState> Current { get; }
    }
}
