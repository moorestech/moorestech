using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit.Send;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の編集の操作。開いているセッションへ渡すか自動運転を送り、開いていなければ拒否してトーストへ
    /// Timetable edit operations: hand edits to the open session or send auto-run, and refuse with a toast when none is open
    /// </summary>
    public sealed class TrainTimetableEditCommands : ITrainTimetableEditCommands
    {
        public const string SetAutoRunCommandName = "train_timetable.set_auto_run";
        private const string LogTag = "TrainTimetableEdit";
        private readonly TrainTimetableEditState _state;
        private readonly ITrainTimetableCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public TrainTimetableEditCommands(TrainTimetableEditState state, ITrainTimetableCommands commands, UiCommandResultToaster toaster)
        {
            _state = state;
            _commands = commands;
            _toaster = toaster;
        }

        // 受理は「編集をセッションへ渡した」の意味。保留中で捨てた編集・並びの同じ編集もセッションがログつきで扱う（Web も知らせない）。サーバーの結果はセッションがトーストへ渡す
        // Accepted means "handed to the session"; edits dropped while pending or with the same order are logged by the session (Web does not notify either), and the session reports the server result
        public UiCommandResult Add(TrainTimetableStation station)
        {
            if (!_state.TryGetSession(out var session)) return NotOpen(TrainTimetableEditSession.ReplaceCommandName);
            session.Replace(TimetableEditLogic.AddStop(session.DisplayedStops, station));
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult Move(int index, StopMoveDirection direction)
        {
            if (!_state.TryGetSession(out var session)) return NotOpen(TrainTimetableEditSession.ReplaceCommandName);
            session.Replace(TimetableEditLogic.MoveStop(session.DisplayedStops, index, direction));
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult Remove(int index)
        {
            if (!_state.TryGetSession(out var session)) return NotOpen(TrainTimetableEditSession.ReplaceCommandName);
            session.Replace(TimetableEditLogic.RemoveStop(session.DisplayedStops, index));
            return UiCommandAccepted.Instance;
        }

        // 自動運転は楽観表示しない。受理は「送った」の意味で、結果は公開で反映され、拒否だけトーストへ
        // Auto-run is never shown optimistically; accepted means "sent", the publication reflects the result and only refusals toast
        public UiCommandResult SetAutoRun(bool enabled)
        {
            if (!_state.TryGetSession(out _)) return NotOpen(SetAutoRunCommandName);
            SendAutoRunAsync(enabled).Forget();
            return UiCommandAccepted.Instance;
        }

        // 時刻表が開いていない操作は送らず、Web ホストが train_not_open を返してトーストするのと同じく拒否をトーストへ（D-32）
        // An operation without an open timetable is not sent and its refusal is toasted, as the Web host's train_not_open is (D-32)
        private UiCommandResult NotOpen(string commandName)
        {
            var rejected = TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);
            _toaster.Report(commandName, rejected);
            return rejected;
        }

        private async UniTaskVoid SendAutoRunAsync(bool enabled)
        {
            var result = await _commands.SetAutoRun(enabled);
            _toaster.Report(SetAutoRunCommandName, result);
        }
    }
}
