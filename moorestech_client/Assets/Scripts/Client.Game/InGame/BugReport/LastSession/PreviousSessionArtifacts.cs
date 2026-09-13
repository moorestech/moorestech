using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションから拾えたものの一覧。拾えなかったものは Missing に理由付きで残す
    // What could be salvaged from the previous session; whatever could not be is listed in Missing with a reason
    public sealed class PreviousSessionArtifacts
    {
        public bool PreviousExitWasClean;
        public string RecordingDirectory;
        public string SnapshotsDirectory;
        public string PlayerLogPath;
        public List<string> CrashDumpFiles = new();
        public List<MissingItem> Missing = new();

        public bool HasAnythingToSend => RecordingDirectory != null || SnapshotsDirectory != null || PlayerLogPath != null || CrashDumpFiles.Count > 0;
    }
}
