using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションから拾えたものの一覧。拾えなかったものは Missing に理由付きで残す
    // What could be salvaged from the previous session; whatever could not be is listed in Missing with a reason
    public sealed class PreviousSessionArtifacts
    {
        // 異常終了したpidが1つも無かったか。pidごとの印を畳んだ結果で、確認ゲートを出すかの唯一の判断材料
        // Whether no pid exited uncleanly; folded from the per-pid marks and the sole basis for showing the confirmation gate
        public bool PreviousExitWasClean;

        // 一度も遊んでいない初回インストール直後。印が無いことを「異常終了」と読まないための区別
        // A brand-new install that has never been played; keeps "no marks" from reading as "crashed"
        public bool IsFirstBoot;

        // 退避できた前回プロセスのpid。0件なら退避物は前世代の持ち越しか、そもそも無い
        // Pids whose files were actually salvaged; zero means the artifacts are a carry-over from an older generation, or absent
        public List<int> SalvagedProcessIds = new();

        public string RecordingDirectory;
        public string SnapshotsDirectory;
        public string PlayerLogPath;
        public List<string> CrashDumpFiles = new();
        public List<MissingItem> Missing = new();

        public bool HasAnythingToSend => RecordingDirectory != null || SnapshotsDirectory != null || PlayerLogPath != null || CrashDumpFiles.Count > 0;
    }
}
