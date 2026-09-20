using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;

namespace Client.Tests.BugReport
{
    // テストが使う退避結果。本番と同じ Clean/Unclean ファクトリだけを通して組み立てる
    // The salvage results tests use, built only through the same Clean/Unclean factories as production
    internal static class TestPreviousSessionArtifacts
    {
        // 未応答の印を消す先。存在しない場所なので消す操作は何もしない成功になり、本番の last-session を触らない
        // Where the pending mark would be cleared; it does not exist, so clearing is a no-op success and the real last-session is never touched
        public static readonly string UnusedLastSessionDirectory = Path.Combine(Path.GetTempPath(), "moorestech-test-unused-last-session");

        public static PreviousSessionArtifacts Clean()
        {
            return PreviousSessionArtifacts.Clean(UnusedLastSessionDirectory, new Dictionary<int, bool>(), new List<MissingItem>());
        }

        public static PreviousSessionArtifacts Unclean()
        {
            return Unclean(UnusedLastSessionDirectory, null, null, null, new List<string>());
        }

        public static PreviousSessionArtifacts UncleanIn(string lastSessionDirectory)
        {
            return Unclean(lastSessionDirectory, null, null, null, new List<string>());
        }

        public static PreviousSessionArtifacts UncleanWithRecording(string recordingDirectory)
        {
            return Unclean(UnusedLastSessionDirectory, recordingDirectory, null, null, new List<string>());
        }

        // 出所はEditor。箱の書き出しが作業ツリーのgit probeを通り、plan C の repository 契約を満たすため
        // The origin is the Editor, so the box goes through the working tree's git probe and meets plan C's repository contract
        public static PreviousSessionArtifacts Unclean(string lastSessionDirectory, string recordingDirectory, string snapshotsDirectory, string playerLogPath, List<string> crashDumpFiles)
        {
            var origin = new SessionOriginSnapshot(null, BuildOriginReading.Editor(), null);
            return PreviousSessionArtifacts.Unclean(lastSessionDirectory, recordingDirectory, snapshotsDirectory, playerLogPath, crashDumpFiles, new List<int>(), new Dictionary<int, bool>(), origin, new List<MissingItem>());
        }
    }
}
