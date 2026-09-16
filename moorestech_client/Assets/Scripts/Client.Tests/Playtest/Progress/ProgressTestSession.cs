using Client.Game.InGame.BugReport.DiskOperations;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using Client.Game.InGame.Playtest.Progress.Storage;

namespace Client.Tests.Playtest
{
    // 進行記録のテストが触る current/ は自プロセスの今回のセッションの段。残骸の作り方を1箇所へ寄せる
    // The current/ the progress tests touch is this process's current session level; how a leftover is fabricated lives in one place
    public static class ProgressTestSession
    {
        public static string Directory => ProgressCurrentSession.DirectoryForCurrentProcess();

        internal static void WriteHeader(ProgressRecordHeader header)
        {
            ProgressRecordFiles.WriteHeader(Directory, header);
        }

        // 追記口を開いて閉じるだけ。記録は書き出さないので、呼んだ後の current/ は「落ちた直後」と同じ状態になる
        // Opens and closes the appender without writing a record, so current/ ends up exactly as it looks right after a crash
        internal static void AppendEvent(IProgressEvent progressEvent)
        {
            var writer = new ProgressSessionWriter();
            writer.Append(progressEvent);
            writer.Dispose();
        }

        // 片付けは段ごとの削除で行う。閉じる手順の内側の ClearCurrent には依存しない
        // Cleanup removes the whole level, independent of the ClearCurrent inside the closing steps
        public static void Clear()
        {
            BugReportDiskOperations.DeleteDirectory(Directory);
        }

        public static bool HasCurrentSession()
        {
            return ProgressRecordFiles.HasCurrentSession(Directory);
        }
    }
}
