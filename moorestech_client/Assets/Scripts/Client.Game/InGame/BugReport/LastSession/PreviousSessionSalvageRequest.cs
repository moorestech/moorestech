using System.Collections.Generic;
using Client.Game.InGame.BugReport.Recording.ProcessScope;

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
        public bool IsRemoteConnection;
        public string WorldSnapshotDirectory;
        public string LastSessionDirectory;
        public List<PreviousProcessSession> PreviousSessions = new();

        // 実行中の他プロセスのpid。退避も削除もせず、欠損理由として報告へ残す対象
        // Pids of processes still running; never salvaged nor deleted, only recorded in the report as a missing reason
        public List<int> SkippedLiveProcessIds = new();
    }

    // 前回プロセスの選別結果。数えてよいpidと、生きているので触らなかったpidを分けて持ち帰る
    // The previous-process selection's result, splitting the pids that may be counted from the live ones left untouched
    public sealed class PreviousProcessScan
    {
        public List<PreviousProcessSession> Sessions = new();
        public List<int> SkippedLiveProcessIds = new();
    }

    // 録画ディレクトリと CLEAN_EXIT 印を、ひとつの生存判定で割る純関数（ADR 0060 裁定2）
    // A pure function splitting the recording directories and the CLEAN_EXIT marks with a single liveness verdict (ADR 0060 adjudication 2)
    // 生存判定を録画ディレクトリの有無に任せると、常時記録オフやffmpeg不在で録画を作らない生きたEditorの印を消し、偽のクラッシュを数える
    // Deriving liveness from the recording directories alone would erase the marks of a live Editor that records nothing (capture off, or no ffmpeg) and count a phantom crash
    public static class PreviousProcessScanner
    {
        public static PreviousProcessScan Scan(int currentProcessId, RecordingProcessTakeover takeover, IReadOnlyList<int> markedProcessIds, ICollection<int> liveProcessIds)
        {
            var scan = new PreviousProcessScan();
            scan.SkippedLiveProcessIds.AddRange(takeover.SkippedLiveProcessIds);

            var recordedProcessIds = new HashSet<int>();
            foreach (var directory in takeover.Directories)
            {
                recordedProcessIds.Add(directory.ProcessId);
                scan.Sessions.Add(new PreviousProcessSession { ProcessId = directory.ProcessId, RecordingDirectory = directory.Path });
            }

            foreach (var processId in markedProcessIds)
            {
                if (processId == currentProcessId || recordedProcessIds.Contains(processId)) continue;
                if (scan.SkippedLiveProcessIds.Contains(processId)) continue;
                if (liveProcessIds.Contains(processId))
                {
                    scan.SkippedLiveProcessIds.Add(processId);
                    continue;
                }
                scan.Sessions.Add(new PreviousProcessSession { ProcessId = processId });
            }
            return scan;
        }
    }
}
