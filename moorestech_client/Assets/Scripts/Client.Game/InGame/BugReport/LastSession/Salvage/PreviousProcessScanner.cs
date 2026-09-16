using System.Collections.Generic;
using Client.Game.InGame.BugReport.Recording.ProcessScope;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションの選別結果。数えてよいセッションと、生きているので触らなかったpidを分けて持ち帰る
    // The previous-session selection's result, splitting the sessions that may be counted from the live pids left untouched
    public sealed class PreviousProcessScan
    {
        public List<PreviousProcessSession> Sessions = new();
        public List<int> SkippedLiveProcessIds = new();
    }

    // 録画ディレクトリと印を、ひとつの生存判定で割る純関数（ADR 0060 裁定2）
    // A pure function splitting the recording directories and the marks with a single liveness verdict (ADR 0060 adjudication 2)
    // 生存判定を録画ディレクトリの有無に任せると、常時記録オフやffmpeg不在で録画を作らない生きたEditorの印を消し、偽のクラッシュを数える
    // Deriving liveness from the recording directories alone would erase the marks of a live Editor that records nothing (capture off, or no ffmpeg) and count a phantom crash
    // 自pidは生存していても、今回以外のセッションは同じプロセスが既に終えたものなので前回として数える（F05）
    // Although this pid is alive, its sessions other than the current one were already finished by this process and count as previous (F05)
    public static class PreviousProcessScanner
    {
        public static PreviousProcessScan Scan(int currentProcessId, string currentSessionName, RecordingProcessTakeover takeover, IReadOnlyList<MarkedSession> markedSessions, ICollection<int> liveProcessIds)
        {
            var scan = new PreviousProcessScan();
            scan.SkippedLiveProcessIds.AddRange(takeover.SkippedLiveProcessIds);

            var recordedSessions = new HashSet<string>();
            foreach (var directory in takeover.Directories)
            {
                recordedSessions.Add(Key(directory.ProcessId, directory.SessionName));
                scan.Sessions.Add(new PreviousProcessSession { ProcessId = directory.ProcessId, SessionName = directory.SessionName, RecordingDirectory = directory.Path });
            }

            foreach (var marked in markedSessions)
            {
                var isCurrentProcess = marked.ProcessId == currentProcessId;
                if (isCurrentProcess && marked.SessionName == currentSessionName) continue;
                if (recordedSessions.Contains(Key(marked.ProcessId, marked.SessionName))) continue;
                if (!isCurrentProcess && IsLive(marked.ProcessId)) continue;
                scan.Sessions.Add(new PreviousProcessSession { ProcessId = marked.ProcessId, SessionName = marked.SessionName });
            }
            return scan;

            #region Internal

            bool IsLive(int processId)
            {
                if (scan.SkippedLiveProcessIds.Contains(processId)) return true;
                if (!liveProcessIds.Contains(processId)) return false;
                scan.SkippedLiveProcessIds.Add(processId);
                return true;
            }

            #endregion
        }

        private static string Key(int processId, string sessionName)
        {
            return $"{processId}/{sessionName}";
        }
    }
}
