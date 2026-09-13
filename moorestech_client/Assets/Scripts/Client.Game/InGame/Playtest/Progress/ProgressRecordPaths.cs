using System;
using System.IO;
using Game.Paths;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行記録の配置規則はここだけが持つ。READY は「record.json まで書き終えた」合図（plan B の outbox と同型）
    // Owns the progress record layout; READY signals record.json is written (same shape as plan B's outbox)
    public static class ProgressRecordPaths
    {
        public const string HeaderFileName = "header.json";
        public const string EventsFileName = "events.jsonl";
        public const string RecordFileName = "record.json";
        public const string ReadyMarkerFileName = "READY";

        public static string CurrentHeaderPath => Path.Combine(GameSystemPaths.ProgressRecordCurrentDirectory, HeaderFileName);
        public static string CurrentEventsPath => Path.Combine(GameSystemPaths.ProgressRecordCurrentDirectory, EventsFileName);

        public static string CreateOutboxDirectory(DateTime now, string shortId)
        {
            var directory = Path.Combine(GameSystemPaths.ProgressRecordOutboxDirectory, $"{now:yyyyMMdd_HHmmss}_{shortId}");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
