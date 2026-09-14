using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Client.Game.InGame.BugReport.Recording.ProcessScope
{
    // 録画リングのプロセス別ディレクトリ。pid_<PID> の綴りと「引き継いでよいのはどれか」をここ1箇所が配る
    // The recording ring's per-process directories; this is the only place that spells pid_<PID> and decides what may be taken over
    public static class RecordingProcessDirectories
    {
        public const string ProcessDirectoryPrefix = "pid_";

        public static int CurrentProcessId()
        {
            return Process.GetCurrentProcess().Id;
        }

        public static string DirectoryFor(string recordingRoot, int processId)
        {
            return Path.Combine(recordingRoot, $"{ProcessDirectoryPrefix}{processId}");
        }

        // 引き継げるのは死んだ他プロセスの分だけ。生存プロセスの録画中ディレクトリを奪うとその実プレイ映像が消える
        // Only a dead other process's directory may be taken over; stealing a live one would erase that session's actual footage
        public static RecordingProcessTakeover TakeOverPreviousProcessDirectories(string recordingRoot, int currentProcessId)
        {
            return SelectTakeover(recordingRoot, currentProcessId, CollectLiveProcessIds());
        }

        // 生存判定を引数で受ける純粋な選別。実プロセスを起こさずに「生きているpidは触らない」を検証できる
        // A pure selection taking liveness as an argument, so "never touch a live pid" is verifiable without spawning processes
        public static RecordingProcessTakeover SelectTakeover(string recordingRoot, int currentProcessId, ICollection<int> liveProcessIds)
        {
            var takeover = new RecordingProcessTakeover();
            if (recordingRoot == null || !Directory.Exists(recordingRoot)) return takeover;

            foreach (var directory in Directory.GetDirectories(recordingRoot))
            {
                var name = Path.GetFileName(directory);
                if (!TryParseProcessId(name, out var processId))
                {
                    takeover.UnknownDirectories.Add(directory);
                    continue;
                }
                if (processId == currentProcessId) continue;
                if (liveProcessIds.Contains(processId))
                {
                    takeover.SkippedLiveProcessIds.Add(processId);
                    continue;
                }
                takeover.Directories.Add(new RecordingProcessDirectory { ProcessId = processId, Path = directory });
            }
            return takeover;
        }

        public static bool TryParseProcessId(string directoryName, out int processId)
        {
            processId = 0;
            if (directoryName == null || !directoryName.StartsWith(ProcessDirectoryPrefix)) return false;
            return int.TryParse(directoryName.Substring(ProcessDirectoryPrefix.Length), out processId);
        }

        // 死んだpidが別プロセスに再利用されていると「生存」と読むが、その場合に触らないのは安全側の誤りなので許容する
        // A recycled pid reads as live, but erring toward leaving it alone is the safe direction, so it is accepted
        // 録画・CLEAN_EXIT印・進行記録のcurrent/は同じこの集合で割る。資源ごとに判定を持つと、録画が無い生存pidだけ素通りする
        // Recordings, the CLEAN_EXIT marks and the progress current/ all split on this one set; a per-resource verdict would let a live pid with no recording slip through
        public static HashSet<int> CollectLiveProcessIds()
        {
            var ids = new HashSet<int>();
            foreach (var process in Process.GetProcesses()) ids.Add(process.Id);
            return ids;
        }
    }
}
