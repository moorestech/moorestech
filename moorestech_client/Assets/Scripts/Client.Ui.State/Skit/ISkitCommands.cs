using Client.Ui.State.Commands;

namespace Client.Ui.State.Skit
{
    /// <summary>
    /// スキットの 5 つの操作（Web の skit.* action）。結果は呼んだ View が UiCommandResultToaster へ渡す
    /// The five skit operations (Web skit.* actions); the calling view hands the result to UiCommandResultToaster
    /// </summary>
    public interface ISkitCommands
    {
        UiCommandResult Advance(string sessionId, int sceneRevision);
        UiCommandResult Select(string sessionId, int sceneRevision, string choiceId);
        UiCommandResult SetAuto(string sessionId, int sceneRevision, bool enabled);
        UiCommandResult Skip(string sessionId, int sceneRevision);
        UiCommandResult SetUiHidden(string sessionId, int sceneRevision, bool hidden);
    }
}
