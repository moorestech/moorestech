using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Client.Game.InGame.BugReport.Recording.ProcessScope
{
    // 録画リング等のプロセス別ディレクトリ。pid_<PID> の綴りと「引き継いでよいのはどれか」をここ1箇所が配る
    // Per-process directories for the recording ring and friends; this is the only place that spells pid_<PID> and decides what may be taken over
    public static class RecordingProcessDirectories
    {
        public const string ProcessDirectoryPrefix = "pid_";

        public static int CurrentProcessId()
        {
            return Process.GetCurrentProcess().Id;
        }

        public static string DirectoryFor(string root, int processId)
        {
            return Path.Combine(root, $"{ProcessDirectoryPrefix}{processId}");
        }

        // 引き継げるのは死んだ他プロセスの全セッションと、自プロセスの今回以外のセッションだけ
        // Only a dead other process's sessions and this process's sessions other than the current one may be taken over
        // 生存プロセスの録画中ディレクトリを奪うとその実プレイ映像が消える。自pidの旧セッションは同じプロセスが既に終えたもの
        // Stealing a live one would erase that session's actual footage; this pid's older sessions were already finished by this very process
        // 生存判定を引数で受ける純粋な選別なので、実プロセスを起こさずに検証できる
        // A pure selection taking liveness as an argument, so it is verifiable without spawning processes
        public static RecordingProcessTakeover SelectTakeover(string root, int currentProcessId, string currentSessionName, ICollection<int> liveProcessIds)
        {
            var takeover = new RecordingProcessTakeover();
            if (root == null || !Directory.Exists(root)) return takeover;

            foreach (var processDirectory in Directory.GetDirectories(root))
            {
                if (!TryParseProcessId(Path.GetFileName(processDirectory), out var processId))
                {
                    takeover.UnknownDirectories.Add(processDirectory);
                    continue;
                }

                var isCurrentProcess = processId == currentProcessId;
                if (!isCurrentProcess && liveProcessIds.Contains(processId))
                {
                    takeover.SkippedLiveProcessIds.Add(processId);
                    continue;
                }
                CollectSessions(processDirectory, processId, isCurrentProcess);
            }
            return takeover;

            #region Internal

            void CollectSessions(string processDirectory, int processId, bool isCurrentProcess)
            {
                foreach (var sessionDirectory in Directory.GetDirectories(processDirectory))
                {
                    var sessionName = Path.GetFileName(sessionDirectory);
                    if (!ProcessSessionScope.IsSessionDirectoryName(sessionName))
                    {
                        takeover.UnknownDirectories.Add(sessionDirectory);
                        continue;
                    }
                    if (isCurrentProcess && sessionName == currentSessionName) continue;
                    takeover.Directories.Add(new RecordingProcessDirectory { ProcessId = processId, SessionName = sessionName, Path = sessionDirectory });
                }
            }

            #endregion
        }

        public static bool TryParseProcessId(string directoryName, out int processId)
        {
            processId = 0;
            if (directoryName == null || !directoryName.StartsWith(ProcessDirectoryPrefix)) return false;
            return int.TryParse(directoryName.Substring(ProcessDirectoryPrefix.Length), out processId);
        }

        // 死んだpidが別プロセスに再利用されていると「生存」と読むが、その場合に触らないのは安全側の誤りなので許容する
        // A recycled pid reads as live, but erring toward leaving it alone is the safe direction, so it is accepted
        // 録画・CLEAN_EXIT印・進行記録のcurrent/は同じこの判定で割る。資源ごとに述語を持つと、録画が無い生存pidだけ素通りする
        // Recordings, the CLEAN_EXIT marks and the progress current/ all split on this one verdict; a per-resource predicate would let a live pid with no recording slip through
        public static HashSet<int> CollectLiveProcessIds()
        {
            var ids = new HashSet<int>();
            foreach (var process in Process.GetProcesses()) ids.Add(process.Id);
            return ids;
        }
    }
}
