using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit.Send;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の編集操作。セッション未開なら拒否してトースト
    /// Timetable edit operations; refused with a toast when no session is open
    /// </summary>
    public sealed class TrainTimetableEditCommands
    {
        private const string SetAutoRunCommandName = "train_timetable.set_auto_run";
        private readonly TrainTimetableEditState _state;
        private readonly ITrainTimetableCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public TrainTimetableEditCommands(TrainTimetableEditState state, ITrainTimetableCommands commands, UiCommandResultToaster toaster)
        {
            _state = state;
            _commands = commands;
            _toaster = toaster;
        }

        // 戻り値は無い（トーストは State が出し、閉じた後の結果は View が消えた後に届くため）。保留中で捨てた編集・並びの同じ編集もセッションがログつきで扱う（Web も知らせない）。サーバーの結果はセッションがトーストへ渡す
        // No return value (State toasts, since results after close arrive once the View is gone); edits dropped while pending or with the same order are logged by the session (Web does not notify either), and the session reports the server result
        public void Add(TrainTimetableStation station)
        {
            if (!_state.TryGetSession(out var session)) { NotOpen(TrainTimetableEditSession.ReplaceCommandName); return; }
            session.Replace(TimetableEditLogic.AddStop(session.DisplayedStops, station));
        }

        public void Move(int index, StopMoveDirection direction)
        {
            if (!_state.TryGetSession(out var session)) { NotOpen(TrainTimetableEditSession.ReplaceCommandName); return; }
            session.Replace(TimetableEditLogic.MoveStop(session.DisplayedStops, index, direction));
        }

        public void Remove(int index)
        {
            if (!_state.TryGetSession(out var session)) { NotOpen(TrainTimetableEditSession.ReplaceCommandName); return; }
            session.Replace(TimetableEditLogic.RemoveStop(session.DisplayedStops, index));
        }

        // 自動運転は楽観表示しない。結果は公開で反映され、拒否だけトーストへ
        // Auto-run is never shown optimistically; the publication reflects the result and only refusals toast
        public void SetAutoRun(bool enabled)
        {
            if (!_state.TryGetSession(out _)) { NotOpen(SetAutoRunCommandName); return; }
            SendAutoRunAsync(enabled).Forget();
        }

        // 時刻表が開いていない操作は送らず、Web ホストが train_not_open を返してトーストするのと同じく拒否をトーストへ（D-32）
        // An operation without an open timetable is not sent and its refusal is toasted, as the Web host's train_not_open is (D-32)
        private void NotOpen(string commandName)
        {
            var rejected = UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);
            _toaster.Report(commandName, rejected);
        }

        private async UniTaskVoid SendAutoRunAsync(bool enabled)
        {
            var result = await _commands.SetAutoRun(enabled);
            _toaster.Report(SetAutoRunCommandName, result);
        }
    }
}
