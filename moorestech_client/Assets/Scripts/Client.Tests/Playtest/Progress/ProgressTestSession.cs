using Client.Game.InGame.Playtest.Progress;

namespace Client.Tests.Playtest
{
    // 進行記録のテストが触る current/ は自プロセスのpidスロット。残骸の作り方を1箇所へ寄せる
    // The current/ the progress tests touch is this process's pid slot; how a leftover is fabricated lives in one place
    public static class ProgressTestSession
    {
        public static string Directory => ProgressCurrentSession.DirectoryForCurrentProcess();

        public static void WriteHeader(ProgressRecordHeader header)
        {
            ProgressRecordFiles.WriteHeader(Directory, header);
        }

        // 追記口を開いて閉じるだけ。記録は書き出さないので、呼んだ後の current/ は「落ちた直後」と同じ状態になる
        // Opens and closes the appender without writing a record, so current/ ends up exactly as it looks right after a crash
        public static void AppendEvent(ProgressEventEntry entry)
        {
            var writer = new ProgressSessionWriter();
            writer.Append(entry);
            writer.Dispose();
        }

        public static void Clear()
        {
            ProgressRecordFiles.ClearCurrent(Directory);
        }

        public static bool HasCurrentSession()
        {
            return ProgressRecordFiles.HasCurrentSession(Directory);
        }
    }
}
