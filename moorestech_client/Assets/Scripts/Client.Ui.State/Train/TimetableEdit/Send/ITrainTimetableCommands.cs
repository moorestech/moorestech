using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// 開いている列車の時刻表への操作。列車はその時点で開いている貨車から引く（Web と同じ）
    /// Operations on the open train's timetable; the train resolves from the car open at that moment (same as Web)
    /// </summary>
    public interface ITrainTimetableCommands
    {
        void RequestOpen();
        UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops);
        UniTask<UiCommandResult> SetAutoRun(bool enabled);
    }
}
