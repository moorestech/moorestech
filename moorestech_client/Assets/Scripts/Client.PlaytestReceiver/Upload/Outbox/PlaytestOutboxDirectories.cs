using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Upload.Attempt;
using Game.Paths;

namespace Client.PlaytestReceiver.Upload
{
    // 走査するoutboxと、その箱の中身の格付けの組。合成ルートが実パスで組み、テストは一時ディレクトリで組む
    // The outboxes to scan, each paired with how its boxes' contents rank; composition roots build it from real paths and tests from temp directories
    public sealed class PlaytestOutboxDirectories
    {
        public readonly string ReportOutbox;
        public readonly IPlaytestBoxFilePolicy ReportFilePolicy;
        public readonly string ProgressOutbox;
        public readonly IPlaytestBoxFilePolicy ProgressFilePolicy;

        public PlaytestOutboxDirectories(string reportOutbox, IPlaytestBoxFilePolicy reportFilePolicy, string progressOutbox, IPlaytestBoxFilePolicy progressFilePolicy)
        {
            ReportOutbox = reportOutbox;
            ReportFilePolicy = reportFilePolicy;
            ProgressOutbox = progressOutbox;
            ProgressFilePolicy = progressFilePolicy;
        }

        public static PlaytestOutboxDirectories FromGameSystemPaths()
        {
            return new PlaytestOutboxDirectories(GameSystemPaths.BugReportOutboxDirectory, new BugReportBoxFilePolicy(), GameSystemPaths.ProgressRecordOutboxDirectory, new ProgressRecordBoxFilePolicy());
        }
    }
}
