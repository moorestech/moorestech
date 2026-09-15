using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Storage
{
    // 起動時に畳んでよい残骸（pid付き）と、触ってはいけない残骸の選別結果
    // What may be folded at boot (with its pid) and what must be left alone
    public sealed class ProgressLeftoverScan
    {
        public readonly List<RecordingProcessDirectory> Sessions = new();
        public readonly List<MissingItem> Skipped = new();
    }

    // current/ をpidで割る。マシン共通の1スロットのままだと、並列起動したAの進行中セッションをBが畳んでイベントが1本に混ざる（ADR 0060 裁定2）
    // Splits current/ by pid; one machine-wide slot would let a parallel boot fold another's live session and mix both event streams into one record (ADR 0060 adjudication 2)
    public static class ProgressCurrentSession
    {
        // 実体は今回のセッションの段（pid_<PID>/session_<utcTicks>/）。同じpidでの再生し直しが前回の書きかけへ追記しないため（F05）
        // Resolves to the current session's level (pid_<PID>/session_<utcTicks>/), so a same-pid replay never appends to the previous half-written session (F05)
        public static string DirectoryForCurrentProcess()
        {
            return ProcessSessionScope.CurrentSessionDirectory(GameSystemPaths.ProgressRecordCurrentDirectory);
        }

        public static string DirectoryFor(int processId)
        {
            return RecordingProcessDirectories.DirectoryFor(GameSystemPaths.ProgressRecordCurrentDirectory, processId);
        }

        // 畳めるのは自分のpidと、既に死んだpidの残骸だけ。生存pidの分は触らず理由を残す
        // Only this pid's and dead pids' leftovers may be folded; a live pid's is left alone with its reason recorded
        public static ProgressLeftoverScan ScanLeftovers()
        {
            var currentProcessId = RecordingProcessDirectories.CurrentProcessId();
            var liveProcessIds = RecordingProcessDirectories.CollectLiveProcessIds();
            var takeover = RecordingProcessDirectories.SelectTakeover(GameSystemPaths.ProgressRecordCurrentDirectory, currentProcessId, ProcessSessionScope.CurrentSessionName, liveProcessIds);
            var scan = new ProgressLeftoverScan();

            // 同じpidの旧セッション（Editorの再生し直し）は takeover が拾う。今回のセッションの段は起動時点では空で、在れば同じく畳む
            // A same-pid older session (an Editor replay) comes through the takeover; the current session's level is empty at boot and is folded too if present
            var ownDirectory = DirectoryForCurrentProcess();
            if (Directory.Exists(ownDirectory)) scan.Sessions.Add(new RecordingProcessDirectory { ProcessId = currentProcessId, SessionName = ProcessSessionScope.CurrentSessionName, Path = ownDirectory });

            scan.Sessions.AddRange(takeover.Directories);
            foreach (var processId in takeover.SkippedLiveProcessIds) AddSkipped(scan, $"pid {processId} は実行中のため進行記録の残骸を畳んでいない（並列起動のセッション）");
            foreach (var directory in takeover.UnknownDirectories) AddSkipped(scan, $"pid_<PID>/session_<utcTicks> の綴りでないディレクトリのため触っていない: {directory}");
            return scan;
        }

        private static void AddSkipped(ProgressLeftoverScan scan, string reason)
        {
            Debug.LogWarning($"進行記録の回収: {reason}");
            scan.Skipped.Add(new MissingItem { Item = "currentSession", Reason = reason });
        }
    }
}
