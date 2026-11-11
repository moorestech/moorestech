using UniRx;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いている列車の時刻表の状態。無ければ Unavailable
    /// Open train's timetable state; Unavailable if none
    /// </summary>
    public interface ITrainTimetableSource
    {
        IReadOnlyReactiveProperty<TrainTimetableViewState> Current { get; }
    }
}
