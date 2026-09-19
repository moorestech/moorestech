using System.IO;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Upload;

namespace Client.Tests.PlaytestReceiver
{
    // READY付きの箱を中身ごと作る。アップロード系テストの共通の下ごしらえ
    // Builds a READY-marked box with its payload; the shared setup of the upload tests
    internal static class PlaytestOutboxTestBoxes
    {
        public static string Make(string outbox, string bundleId, params (string Name, string Content)[] files)
        {
            var box = Path.Combine(outbox, bundleId);
            Directory.CreateDirectory(box);
            foreach (var file in files) File.WriteAllText(Path.Combine(box, file.Name), file.Content);
            File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.ReadyMarker), "");
            return box;
        }

        // 一時ルートの下に本番と同じ格付けの組でoutboxを2つ作る
        // Creates the two outboxes under a temp root, paired with the same rankings as production
        public static PlaytestOutboxDirectories Directories(string root)
        {
            var directories = new PlaytestOutboxDirectories(Path.Combine(root, "BugReports", "outbox"), new BugReportBoxFilePolicy(), Path.Combine(root, "ProgressRecords", "outbox"), new ProgressRecordBoxFilePolicy());
            Directory.CreateDirectory(directories.ReportOutbox);
            Directory.CreateDirectory(directories.ProgressOutbox);
            return directories;
        }
    }
}
