using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 起動直後に前回の記録を退避する。内蔵サーバーのリングと録画リングが上書きを始める前に走らせること
    // Salvages the previous records right at boot, before the embedded server's ring and the recording ring start overwriting
    // 単位は pid_<PID>/session_<utcTicks>。生存している他プロセスの記録は触らず、触らなかった理由を報告へ残す
    // The unit is pid_<PID>/session_<utcTicks>: a live process's records are left alone, and the reason for leaving them is kept in the report
    public static class PreviousSessionSalvage
    {
        // 触らなかった生存pidの欠損名。録画の欠損と混ぜないための独立した綴り
        // The item name for a live pid that was left alone; a separate spelling so it never mixes with a missing recording
        public const string LiveProcessMissingItem = "liveProcess";

        // 退避した資料の出所。未応答の資料を後の起動で再提示するときも同じ出所を載せるため last-session に永続化する
        // The salvaged evidence's origin, persisted in last-session so a later boot re-presenting unanswered evidence carries the same origin
        public const string PreviousOriginFileName = "previous-origin.json";

        private static PreviousSessionArtifacts _artifacts;

        // 呼ぶ前に ProcessSessionScope.BeginNewSession() でこの起動のセッション名を確定しておくこと
        // ProcessSessionScope.BeginNewSession() must have fixed this boot's session name before this is called
        public static PreviousSessionArtifacts RunAtStartup()
        {
            var lastSessionDirectory = GameSystemPaths.BugReportLastSessionDirectory;

            var currentProcessId = RecordingProcessDirectories.CurrentProcessId();
            var currentSessionName = ProcessSessionScope.CurrentSessionName;

            // 印を先に読んでから生存集合を採る。逆順だと、集合を採った後に起動して印を書いたpidが集合に居ず、死んだ前回セッションとして畳まれる
            // The marks are read before the liveness set; the reverse order would miss a pid that booted and marked itself after the set was taken, folding a live session as a dead one
            var markedSessions = CleanExitMarker.MarkedSessions();

            // 生存判定は3資源（録画・印・current/）で1つ。ここで1回だけ採り、録画と印の両方へ同じ集合を当てる
            // One liveness set serves all three resources (recordings, marks, current/); taken once here and applied to recordings and marks alike
            var liveProcessIds = RecordingProcessDirectories.CollectLiveProcessIds();
            var takeover = RecordingProcessDirectories.SelectTakeover(GameSystemPaths.BugReportRecordingDirectory, currentProcessId, currentSessionName, liveProcessIds);
            var scan = PreviousProcessScanner.Scan(currentProcessId, currentSessionName, takeover, markedSessions, liveProcessIds);

            var request = new PreviousSessionSalvageRequest
            {
                LastSessionDirectory = lastSessionDirectory,
                SkippedLiveProcessIds = scan.SkippedLiveProcessIds,
                PreviousSessions = ConsumeExitMarks(scan.Sessions),
            };

            _artifacts = Salvage(request);
            Debug.Log($"前回セッションの退避が終わりました clean:{_artifacts.PreviousExitWasClean} sessions:{request.PreviousSessions.Count} salvagedPids:{_artifacts.SalvagedProcessIds.Count} sendable:{_artifacts.HasAnythingToSend} missing:{_artifacts.Missing.Count}");
            return _artifacts;
        }

        // 退避結果の唯一の窓口。退避より前に到達するのは起動順の契約違反なので、正常終了の値で偽装せず例外にする（F13）
        // The single window onto the salvage result; arriving before the salvage breaks the boot-order contract, so it throws instead of posing as a clean exit (F13)
        public static PreviousSessionArtifacts RequireArtifacts()
        {
            if (_artifacts != null) return _artifacts;
            throw new InvalidOperationException("PreviousSessionSalvage: 退避が未実行のまま結果が要求されました（起動順が変わり PreviousSessionStartupTasks より前へ到達しています）");
        }

        public static PreviousSessionArtifacts Salvage(PreviousSessionSalvageRequest request)
        {
            var missing = new SalvageMissingLog();
            foreach (var processId in request.SkippedLiveProcessIds)
            {
                // itemは recording ではなく liveProcess。常時記録オフの並列Editorは録画自体を作らないため、recording名義だと「録画が欠けた」と読めてしまう
                // The item is liveProcess rather than recording: a parallel capture-off Editor makes no recording at all, so the recording name would read as "the footage is missing"
                missing.Report(LiveProcessMissingItem, $"pid {processId} は実行中のため退避も削除もしていない（並列起動のセッション）");
            }

            var creation = BugReportDiskOperations.CreateDirectory(request.LastSessionDirectory);
            if (!creation.Succeeded) missing.Report("lastSession", creation.FailureReason);

            var exitedCleanlyByProcessId = new Dictionary<int, bool>();
            var uncleanSessions = new List<PreviousProcessSession>();
            foreach (var session in request.PreviousSessions) SortSession(session);

            // 未応答の印は異常終了を検知した時点で置く。ゲートを出さない起動でも置き、ゲートに答えた起動だけが消す（F04）
            // The pending mark is placed as soon as an unclean exit is detected, even on launches that show no gate, and only an answered gate clears it (F04)
            if (0 < uncleanSessions.Count) PendingCrashReportMark.MarkPending(request.LastSessionDirectory);
            var carriesPendingReport = uncleanSessions.Count == 0 && PendingCrashReportMark.IsPending(request.LastSessionDirectory);

            // 正常終了で未応答の資料も無ければ前回分は要らない。前世代の退避物も含めて空にし、1世代だけ保持する規律を毎回満たす
            // A clean exit with nothing unanswered needs nothing kept: the previous generation is emptied too, so "keep exactly one generation" holds every boot
            if (uncleanSessions.Count == 0 && !carriesPendingReport)
            {
                ClearPreviousGeneration(Path.Combine(request.LastSessionDirectory, BugReportBundleLayout.RecordingDirectoryName), missing);
                ClearPreviousGeneration(Path.Combine(request.LastSessionDirectory, BugReportBundleLayout.SnapshotDirectoryName), missing);
                var originDeletion = BugReportFileOperations.DeleteFile(Path.Combine(request.LastSessionDirectory, PreviousOriginFileName));
                if (!originDeletion.Succeeded) missing.Report("previousOrigin", $"前世代の出所を消せなかった: {originDeletion.FailureReason}");
                return PreviousSessionArtifacts.Clean(request.LastSessionDirectory, exitedCleanlyByProcessId, missing.Items);
            }

            return UncleanSessionSalvage.Collect(request, uncleanSessions, exitedCleanlyByProcessId, carriesPendingReport, missing);

            #region Internal

            void SortSession(PreviousProcessSession session)
            {
                var foldedSoFar = !exitedCleanlyByProcessId.TryGetValue(session.ProcessId, out var previous) || previous;
                exitedCleanlyByProcessId[session.ProcessId] = foldedSoFar && session.ExitedCleanly;

                if (session.ExitedCleanly)
                {
                    DiscardCleanSessionRecording(session);
                    return;
                }
                uncleanSessions.Add(session);
                if (session.ShutdownStalled) missing.Report("shutdown", $"pid {session.ProcessId} {session.SessionName} は終了処理の途中で止まった（終了の意思表明はあるが書き出し完了の印が無い）");
            }

            void DiscardCleanSessionRecording(PreviousProcessSession session)
            {
                if (session.RecordingDirectory == null) return;
                var deletion = BugReportDiskOperations.DeleteDirectory(session.RecordingDirectory);
                if (!deletion.Succeeded) missing.Report(BugReportBundleLayout.RecordingDirectoryName, $"pid {session.ProcessId} {session.SessionName} の録画を消せなかった: {deletion.FailureReason}");
                DeleteEmptiedProcessDirectory(session, missing);
            }

            #endregion
        }

        // セッションの段を畳んだ後の空の pid_<PID> を片付ける。残すと recording/ に積み上がり、起動ごとの全走査が単調に重くなる
        // Clears the pid_<PID> left empty after its session was folded; leaving it piles them up in recording/ and makes every boot's scan heavier
        internal static void DeleteEmptiedProcessDirectory(PreviousProcessSession session, SalvageMissingLog missing)
        {
            var deletion = BugReportDiskOperations.DeleteDirectoryIfEmpty(Path.GetDirectoryName(session.RecordingDirectory));
            if (!deletion.Succeeded) missing.Report(BugReportBundleLayout.RecordingDirectoryName, $"pid {session.ProcessId} の空ディレクトリを消せなかった: {deletion.FailureReason}");
        }

        internal static void ClearPreviousGeneration(string destination, SalvageMissingLog missing)
        {
            var clearing = BugReportDiskOperations.ClearDirectory(destination);
            if (!clearing.Succeeded) missing.Report(Path.GetFileName(destination), $"前世代の退避物を消せなかった: {clearing.FailureReason}");
        }

        // 印を消してよいのは選別を通ったセッションだけ。生きているpidの印まで消すと、そのプロセスが本当に落ちても次回起動で検知できない
        // Only the sessions that passed the selection may have their marks removed; erasing a live pid's would leave its real crash undetectable next boot
        private static List<PreviousProcessSession> ConsumeExitMarks(List<PreviousProcessSession> sessions)
        {
            foreach (var session in sessions)
            {
                var record = CleanExitMarker.ConsumeSessionMarks(session.ProcessId, session.SessionName);
                session.ExitedCleanly = record.ExitedCleanly;
                session.ShutdownStalled = record.ShutdownStalled;
                session.Origin = record.Origin;
                session.OriginMissingReason = record.OriginMissingReason;
            }
            return sessions;
        }
    }
}
