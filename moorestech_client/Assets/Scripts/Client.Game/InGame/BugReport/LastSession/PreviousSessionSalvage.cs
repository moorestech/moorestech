using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 起動直後に前回の記録を退避する。内蔵サーバーのリングと録画リングが上書きを始める前に走らせること
    // Salvages the previous records right at boot, before the embedded server's ring and the recording ring start overwriting
    // 単位はpid。生存している他プロセスの記録は触らず、触らなかった理由を報告へ残す
    // The unit is the pid: a live process's records are left alone, and the reason for leaving them is kept in the report
    public static class PreviousSessionSalvage
    {
        // 触らなかった生存pidの欠損名。録画の欠損と混ぜないための独立した綴り
        // The item name for a live pid that was left alone; a separate spelling so it never mixes with a missing recording
        public const string LiveProcessMissingItem = "liveProcess";

        public static PreviousSessionArtifacts Artifacts { get; private set; }

        public static PreviousSessionArtifacts RunAtStartup(bool isRemoteConnection, string worldSnapshotDirectory)
        {
            var lastSessionDirectory = GameSystemPaths.BugReportLastSessionDirectory;

            // 印もディレクトリも無い＝初回インストール直後。異常終了と読むと、一度も遊んでいないテスターに確認ゲートが出る
            // No marks and no directory means a fresh install; reading that as a crash would show the gate to a tester who never played
            var isFirstBoot = !Directory.Exists(lastSessionDirectory);

            var currentProcessId = RecordingProcessDirectories.CurrentProcessId();

            // 印を先に読んでから生存集合を採る。逆順だと、集合を採った後に起動して印を書いたpidが集合に居ず、死んだ前回セッションとして畳まれる
            // The marks are read before the liveness set; the reverse order would miss a pid that booted and marked itself after the set was taken, folding a live session as a dead one
            var markedProcessIds = CleanExitMarker.SessionProcessIds();

            // 生存判定は3資源（録画・印・current/）で1つ。ここで1回だけ採り、録画と印の両方へ同じ集合を当てる
            // One liveness set serves all three resources (recordings, marks, current/); taken once here and applied to recordings and marks alike
            var liveProcessIds = RecordingProcessDirectories.CollectLiveProcessIds();
            var takeover = RecordingProcessDirectories.SelectTakeover(GameSystemPaths.BugReportRecordingDirectory, currentProcessId, liveProcessIds);
            var scan = PreviousProcessScanner.Scan(currentProcessId, takeover, markedProcessIds, liveProcessIds);

            var request = new PreviousSessionSalvageRequest
            {
                IsRemoteConnection = isRemoteConnection,
                WorldSnapshotDirectory = worldSnapshotDirectory,
                LastSessionDirectory = lastSessionDirectory,
                SkippedLiveProcessIds = scan.SkippedLiveProcessIds,
                PreviousSessions = ConsumeExitFlags(scan.Sessions),
            };

            Artifacts = Salvage(request);
            Debug.Log($"前回セッションの退避が終わりました clean:{Artifacts.PreviousExitWasClean} firstBoot:{isFirstBoot} salvagedPids:{Artifacts.SalvagedProcessIds.Count} sendable:{Artifacts.HasAnythingToSend} missing:{Artifacts.Missing.Count}");
            return Artifacts;
        }

        // 退避が未実行のまま判定が要るときの省略時規則。極性と警告をここ1箇所へ集め、後から1行で反転できるようにする
        // The default rule when a verdict is needed before the salvage ran; the polarity and its warning live here so one line can flip them
        public static PreviousSessionArtifacts ArtifactsOrNotRunDefault()
        {
            if (Artifacts != null) return Artifacts;
            Debug.LogWarning("PreviousSessionSalvage: 退避が未実行のため「前回は正常終了」として扱います（起動順が変わり退避より前へ到達しています）");
            return new PreviousSessionArtifacts { PreviousExitWasClean = true };
        }

        public static PreviousSessionArtifacts Salvage(PreviousSessionSalvageRequest request)
        {
            var artifacts = new PreviousSessionArtifacts();
            ReportSkippedLiveProcesses();

            var creation = SalvageFileOperations.CreateDirectory(request.LastSessionDirectory);
            if (!creation.Succeeded) ReportMissing("lastSession", creation.FailureReason);

            var recordingDestination = Path.Combine(request.LastSessionDirectory, BugReportBundleLayout.RecordingDirectoryName);
            var snapshotDestination = Path.Combine(request.LastSessionDirectory, BugReportBundleLayout.SnapshotDirectoryName);

            var uncleanSessions = new List<PreviousProcessSession>();
            foreach (var session in request.PreviousSessions)
                if (session.ExitedCleanly) DiscardCleanSessionRecording(session);
                else uncleanSessions.Add(session);

            artifacts.PreviousExitWasClean = uncleanSessions.Count == 0;

            // 正常終了なら前回分は要らない。前世代の退避物も含めて空にし、1世代だけ保持する規律を毎回満たす
            // A clean exit needs nothing kept: the previous generation is emptied too, so "keep exactly one generation" holds every boot
            if (artifacts.PreviousExitWasClean)
            {
                ClearPreviousGeneration(recordingDestination);
                ClearPreviousGeneration(snapshotDestination);
                return artifacts;
            }

            MoveUncleanRecordings(uncleanSessions, recordingDestination);
            MoveWorldSnapshots(snapshotDestination);

            artifacts.RecordingDirectory = ResolveSalvagedDirectory(recordingDestination, BugReportBundleLayout.RecordingDirectoryName);
            artifacts.SnapshotsDirectory = ResolveSalvagedDirectory(snapshotDestination, BugReportBundleLayout.SnapshotDirectoryName);

            artifacts.PlayerLogPath = PlayerLogLocator.PreviousSessionLogPath();
            if (artifacts.PlayerLogPath == null) ReportMissing("playerLog", "前回セッションのPlayer-prev.logが見つからない");

            var crashDumpScan = CrashDumpLocator.FindDumpFiles();
            artifacts.CrashDumpFiles = crashDumpScan.Files;
            if (artifacts.CrashDumpFiles.Count == 0) ReportMissing("crashDump", CrashDumpLocator.MissingReason(crashDumpScan));

            return artifacts;

            #region Internal

            void ReportSkippedLiveProcesses()
            {
                foreach (var processId in request.SkippedLiveProcessIds)
                {
                    var reason = $"pid {processId} は実行中のため退避も削除もしていない（並列起動のセッション）";
                    Debug.LogWarning($"前回セッションの退避: {reason}");

                    // itemは recording ではなく liveProcess。常時記録オフの並列Editorは録画自体を作らないため、recording名義だと「録画が欠けた」と読めてしまう
                    // The item is liveProcess rather than recording: a parallel capture-off Editor makes no recording at all, so the recording name would read as "the footage is missing"
                    artifacts.Missing.Add(new MissingItem { Item = LiveProcessMissingItem, Reason = reason });
                }
            }

            void DiscardCleanSessionRecording(PreviousProcessSession session)
            {
                if (session.RecordingDirectory == null) return;
                var deletion = SalvageFileOperations.DeleteDirectory(session.RecordingDirectory);
                if (!deletion.Succeeded) ReportMissing(BugReportBundleLayout.RecordingDirectoryName, $"pid {session.ProcessId} の録画を消せなかった: {deletion.FailureReason}");
            }

            void ClearPreviousGeneration(string destination)
            {
                var clearing = SalvageFileOperations.ClearDirectory(destination);
                if (!clearing.Succeeded) ReportMissing(Path.GetFileName(destination), $"前世代の退避物を消せなかった: {clearing.FailureReason}");
            }

            // 新しい退避物があるときだけ退避先を空にする。無いまま空にすると、前世代の本物のクラッシュ資料が二度と提示されない
            // The destination is emptied only when new files arrive; emptying it regardless would drop an older real crash's evidence for good
            void MoveUncleanRecordings(List<PreviousProcessSession> sessions, string destination)
            {
                var hasNewRecording = false;
                foreach (var session in sessions) hasNewRecording |= session.RecordingDirectory != null;
                if (!hasNewRecording) return;

                ClearPreviousGeneration(destination);
                foreach (var session in sessions)
                {
                    if (session.RecordingDirectory == null) continue;
                    var processDirectoryName = RecordingProcessDirectories.ProcessDirectoryPrefix + session.ProcessId;
                    var move = SalvageFileOperations.MoveDirectory(session.RecordingDirectory, Path.Combine(destination, processDirectoryName));
                    if (move.Succeeded) artifacts.SalvagedProcessIds.Add(session.ProcessId);
                    else ReportMissing(BugReportBundleLayout.RecordingDirectoryName, $"pid {session.ProcessId}: {move.FailureReason}");
                }
            }

            // リモート接続にはスナップショットを書く内蔵サーバーがそもそも居ない。退避失敗と同じ理由文にすると毎回「失敗」に見える
            // A remote connection has no embedded server writing snapshots at all; sharing the failure wording would read as a failure every time
            void MoveWorldSnapshots(string destination)
            {
                if (request.IsRemoteConnection)
                {
                    ReportMissing(BugReportBundleLayout.SnapshotDirectoryName, "リモート接続のセッションのため内蔵サーバーのスナップショットは存在しない");
                    return;
                }

                var move = SalvageFileOperations.MoveFilesInto(request.WorldSnapshotDirectory, destination);
                if (!move.Succeeded) ReportMissing(BugReportBundleLayout.SnapshotDirectoryName, move.FailureReason);
            }

            // 退避先に中身があれば、今回移した分でも前世代の持ち越しでも同じく提示する（次回起動で聞き直せるという約束を守る）
            // Whatever sits in the destination is presented, newly moved or carried over, keeping the promise that the next boot can ask again
            string ResolveSalvagedDirectory(string destination, string item)
            {
                var probe = SalvageFileOperations.ProbeHasAnyFile(destination);
                if (probe.Succeeded) return destination;
                ReportMissing(item, probe.FailureReason);
                return null;
            }

            // 欠損は必ず開発者ログと報告の両方へ積む。片方だけだと拒否理由が誰にも届かない
            // Every gap lands in both the developer log and the report; one alone leaves the reason unreachable
            void ReportMissing(string item, string reason)
            {
                Debug.LogWarning($"前回セッションの退避で欠損 {item}: {reason}");
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = reason });
            }

            #endregion
        }

        // 印を消してよいのは選別を通ったpidだけ。生きているpidの印まで消すと、そのプロセスが本当に落ちても次回起動で検知できない
        // Only the pids that passed the selection may have their marks removed; erasing a live pid's would leave its real crash undetectable next boot
        private static List<PreviousProcessSession> ConsumeExitFlags(List<PreviousProcessSession> sessions)
        {
            foreach (var session in sessions) session.ExitedCleanly = CleanExitMarker.ConsumeExitCleanFlag(session.ProcessId);
            return sessions;
        }
    }
}
