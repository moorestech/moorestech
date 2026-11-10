using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の編集の操作。View が直接呼ぶ（設計書 A-8。Presenter は中継しない）。結果のトーストはこの口の実装が済ませる
    /// Timetable edit operations; the View calls them directly (design doc A-8; no Presenter relay) and the implementation reports results to the toaster
    /// </summary>
    public interface ITrainTimetableEditCommands
    {
        UiCommandResult Add(TrainTimetableStation station);
        UiCommandResult Move(int index, StopMoveDirection direction);
        UiCommandResult Remove(int index);
        UiCommandResult SetAutoRun(bool enabled);
    }
}
