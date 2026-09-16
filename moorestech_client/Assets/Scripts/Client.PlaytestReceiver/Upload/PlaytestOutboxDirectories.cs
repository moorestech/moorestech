using Game.Paths;

namespace Client.PlaytestReceiver.Upload
{
    // 走査するoutboxの組。合成ルートが実パスで組み、テストは一時ディレクトリで組む
    // The pair of outboxes to scan; composition roots build it from real paths and tests from temp directories
    public sealed class PlaytestOutboxDirectories
    {
        public readonly string ReportOutbox;
        public readonly string ProgressOutbox;

        public PlaytestOutboxDirectories(string reportOutbox, string progressOutbox)
        {
            ReportOutbox = reportOutbox;
            ProgressOutbox = progressOutbox;
        }

        public static PlaytestOutboxDirectories FromGameSystemPaths()
        {
            return new PlaytestOutboxDirectories(GameSystemPaths.BugReportOutboxDirectory, GameSystemPaths.ProgressRecordOutboxDirectory);
        }
    }
}
