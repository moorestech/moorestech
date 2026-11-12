using Client.Skit.UI;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Skit
{
    /// <summary>
    /// 操作を store の Try* へ写す。session・版・許可の検証は store が 1 か所で行い、失敗コードは wire の上書きとして返す（A-84・A-260）
    /// Maps operations onto the store's Try* methods; the store validates session, revision and allowance in one place, and the failure code returns as the wire code override (A-84, A-260)
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

        public SkitCommands(SkitPresentationStateStore store)
        {
            _store = store;
        }

        public UiCommandResult Advance(string sessionId, int sceneRevision)
        {
            return ToResult(_store.TryAdvance(sessionId, sceneRevision));
        }

        public UiCommandResult Select(string sessionId, int sceneRevision, string choiceId)
        {
            return ToResult(_store.TrySelect(sessionId, sceneRevision, choiceId));
        }

        public UiCommandResult SetAuto(string sessionId, int sceneRevision, bool enabled)
        {
            return ToResult(_store.TrySetAuto(sessionId, sceneRevision, enabled));
        }

        public UiCommandResult Skip(string sessionId, int sceneRevision)
        {
            return ToResult(_store.TrySkip(sessionId, sceneRevision));
        }

        public UiCommandResult SetUiHidden(string sessionId, int sceneRevision, bool hidden)
        {
            return ToResult(_store.TrySetUiHidden(sessionId, sceneRevision, hidden));
        }

        private static UiCommandResult ToResult(SkitIntentResult result)
        {
            if (result.Ok) return UiCommandAccepted.Instance;
            return UiCommandRejected.OverridingWireCode(UiCommandRejectReason.SkitIntentRejected, result.Error);
        }
    }
}
