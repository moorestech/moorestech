using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回のプロセス1つぶんの終了状態。正常終了マーカーと録画ディレクトリはどちらもpidで割られている
    // One previous process's exit state; both the clean-exit marker and the recording directory are split by pid
    public sealed class PreviousProcessSession
    {
        public int ProcessId;
        public bool ExitedCleanly;
        public string RecordingDirectory;
    }

    // 退避の入力。生存プロセスの判定と印の回収は呼び出し側（RunAtStartup）で済ませ、ここには結果だけを渡す
    // The salvage's input; liveness and marker consumption happen in RunAtStartup, and only their results arrive here
    public sealed class PreviousSessionSalvageRequest
    {
        public bool IsFirstBoot;
        public bool IsRemoteConnection;
        public string WorldSnapshotDirectory;
        public string LastSessionDirectory;
        public List<PreviousProcessSession> PreviousSessions = new();

        // 実行中の他プロセスのpid。退避も削除もせず、欠損理由として報告へ残す対象
        // Pids of processes still running; never salvaged nor deleted, only recorded in the report as a missing reason
        public List<int> SkippedLiveProcessIds = new();
    }
}
