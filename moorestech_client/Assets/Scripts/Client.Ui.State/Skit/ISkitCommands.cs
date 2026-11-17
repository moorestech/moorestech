namespace Client.Ui.State.Skit
{
    /// <summary>
    /// スキットの 5 つの操作（Web の skit.* action）。結果は実装がトーストへ渡す
    /// The five skit operations (Web skit.* actions); the implementation hands results to the toaster
    /// </summary>
    public interface ISkitCommands
    {
        void Advance(string sessionId, int sceneRevision);
        void Select(string sessionId, int sceneRevision, string choiceId);
        void SetAuto(string sessionId, int sceneRevision, bool enabled);
        void Skip(string sessionId, int sceneRevision);
        void SetUiHidden(string sessionId, int sceneRevision, bool hidden);
    }
}
