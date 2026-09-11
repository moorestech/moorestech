using System;
using System.IO;
using Game.Paths;

namespace Client.Game.InGame.BugReport
{
    // outbox の配置規則はここだけが持つ。READY は「manifest まで書き終えた」合図で、運搬側はこれが無い箱を触らない
    // Owns the outbox layout; READY signals the manifest is written, and the shipper ignores boxes without it
    public static class BugReportOutbox
    {
        public const string ReadyMarkerFileName = "READY";

        public static string CreateBundleDirectory(DateTime now, string shortId)
        {
            var directory = Path.Combine(GameSystemPaths.BugReportOutboxDirectory, $"{now:yyyyMMdd_HHmmss}_{shortId}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        public static void MarkReady(string bundleDirectory)
        {
            File.WriteAllText(Path.Combine(bundleDirectory, ReadyMarkerFileName), "");
        }
    }
}
