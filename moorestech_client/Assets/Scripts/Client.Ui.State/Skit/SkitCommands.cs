using Client.Skit.UI;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;

namespace Client.Ui.State.Skit
{
    /// <summary>
    /// 操作を store の Try* へ写し、結果をトーストへ渡す（A-84）
    /// Maps operations to store Try* methods and hands results to the toaster (A-84, A-260)
    /// </summary>
    public sealed class SkitCommands : ISkitCommands
    {
        // トーストに渡すコマンド名。Web の action 名と同じ綴り
        // Command names passed to the toaster; spelled like the Web action names
        public const string AdvanceCommandName = "skit.advance";
        public const string SelectCommandName = "skit.select";
        public const string SetAutoCommandName = "skit.set_auto";
        public const string SkipCommandName = "skit.skip";
        public const string SetUiHiddenCommandName = "skit.set_ui_hidden";

        private readonly SkitPresentationStateStore _store;
        private readonly IUiCommandResultToaster _toaster;

        public SkitCommands(SkitPresentationStateStore store, IUiCommandResultToaster toaster)
        {
            _store = store;
            _toaster = toaster;
        }

        public void Advance(string sessionId, int sceneRevision)
        {
            Report(AdvanceCommandName, _store.TryAdvance(sessionId, sceneRevision));
        }

        public void Select(string sessionId, int sceneRevision, string choiceId)
        {
            Report(SelectCommandName, _store.TrySelect(sessionId, sceneRevision, choiceId));
        }

        public void SetAuto(string sessionId, int sceneRevision, bool enabled)
        {
            Report(SetAutoCommandName, _store.TrySetAuto(sessionId, sceneRevision, enabled));
        }

        public void Skip(string sessionId, int sceneRevision)
        {
            Report(SkipCommandName, _store.TrySkip(sessionId, sceneRevision));
        }

        public void SetUiHidden(string sessionId, int sceneRevision, bool hidden)
        {
            Report(SetUiHiddenCommandName, _store.TrySetUiHidden(sessionId, sceneRevision, hidden));
        }

        private void Report(string commandName, SkitIntentResult result)
        {
            var commandResult = result.Ok ? (UiCommandResult)UiCommandAccepted.Instance : UiCommandRejected.OverridingWireCode(UiCommandRejectReason.SkitIntentRejected, result.Error);
            _toaster.Report(commandName, commandResult);
        }
    }
}
