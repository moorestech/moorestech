using System.IO;

namespace Client.Game.InGame.Playtest.Progress.Storage
{
    // 進行記録の1セッションぶんのファイル名。箱の綴りと READY は plan B の outbox（BugReportOutbox）が持つ
    // The file names of one progress session; the box naming and READY belong to plan B's outbox (BugReportOutbox)
    public static class ProgressRecordPaths
    {
        public const string HeaderFileName = "header.json";
        public const string EventsFileName = "events.jsonl";
        public const string RecordFileName = "record.json";

        public static string HeaderPathIn(string currentSessionDirectory)
        {
            return Path.Combine(currentSessionDirectory, HeaderFileName);
        }

        public static string EventsPathIn(string currentSessionDirectory)
        {
            return Path.Combine(currentSessionDirectory, EventsFileName);
        }
    }
}
