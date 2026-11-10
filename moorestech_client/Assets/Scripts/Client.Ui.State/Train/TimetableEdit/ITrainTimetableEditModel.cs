using UniRx;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表タブの描画値。Presenter が購読して View へ流す（操作は ITrainTimetableEditCommands）
    /// Draw values of the timetable tab; the Presenter subscribes them into the View (operations live in ITrainTimetableEditCommands)
    /// </summary>
    public interface ITrainTimetableEditModel
    {
        IReadOnlyReactiveProperty<TrainTimetableAreaViewModel> Current { get; }
    }
}
