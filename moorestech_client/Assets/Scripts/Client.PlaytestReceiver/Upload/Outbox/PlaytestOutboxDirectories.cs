using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Upload.Attempt;
using Game.Paths;

namespace Client.PlaytestReceiver.Upload
{
    // outboxと箱内パス格付けの組。合成ルートが実パスで組む
    // Pairs each outbox with its content ranking; composition roots build it from real paths
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
