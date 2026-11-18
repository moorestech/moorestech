namespace Client.Game.InGame.BugReport
{
    // 箱を作る outbox の根。テストは一時ディレクトリを渡し、運搬役が読む本番 outbox に READY 箱を残さない
    // The outbox root boxes are created under; tests pass a temp directory so no READY box lands in the production outbox the uploader reads
    public interface IBugReportOutboxRoot
    {
        string RootDirectory { get; }
    }

    public sealed class GameSystemBugReportOutboxRoot : IBugReportOutboxRoot
    {
        public string RootDirectory => BugReportOutbox.DefaultRootDirectory;
    }
}
