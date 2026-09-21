using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 確認ゲートへ出す資料を集める側。今回の異常終了を退避するか、未応答の前世代をそのまま再提示するかの2通り
    // Gathers the evidence for the confirmation gate, either salvaging this boot's detected crash or re-presenting an unanswered older generation as-is
    internal static class UncleanSessionSalvage
    {
        public static PreviousSessionArtifacts Collect(PreviousSessionSalvageRequest request, List<PreviousProcessSession> uncleanSessions, IReadOnlyDictionary<int, bool> exitedCleanlyByProcessId, bool carriesPendingReport, SalvageMissingLog missing)
        {
            var recordingDestination = Path.Combine(request.LastSessionDirectory, BugReportBundleLayout.RecordingDirectoryName);
            var snapshotDestination = Path.Combine(request.LastSessionDirectory, BugReportBundleLayout.SnapshotDirectoryName);
            var originPath = Path.Combine(request.LastSessionDirectory, PreviousSessionSalvage.PreviousOriginFileName);
            var salvagedProcessIds = new List<int>();
            SessionOriginSnapshot previousOrigin;
            string playerLogPath;
            var hasOwnedSnapshots = carriesPendingReport;

            if (carriesPendingReport)
            {
                // 前回は正常終了だが、それより前のクラッシュ資料にまだ誰も答えていない。移動も上書きもせず前世代をそのまま提示し直す（F04）
                // The last exit was clean, yet nobody answered an older crash's evidence; nothing is moved or overwritten and that generation is presented again (F04)
                Debug.Log("前回異常終了の確認が未応答のため、last-session に残る前世代の資料を再提示します");
                previousOrigin = ReadPersistedOrigin();
                hasOwnedSnapshots = SnapshotOwnershipMatches(snapshotDestination);
                playerLogPath = null;
                missing.Report("playerLog", "未応答のクラッシュ資料を再提示する起動のため、その後の正常終了セッションが Player-prev.log を上書き済み");
            }
            else
            {
                MoveUncleanRecordings();
                previousOrigin = PersistLatestOrigin();
                hasOwnedSnapshots = MoveWorldSnapshots();
                playerLogPath = PlayerLogLocator.PreviousSessionLogPath();
                if (playerLogPath == null) missing.Report("playerLog", "前回セッションのPlayer-prev.logが見つからない");
            }

            var recordingDirectory = ResolveSalvagedDirectory(recordingDestination, BugReportBundleLayout.RecordingDirectoryName);
            var snapshotsDirectory = hasOwnedSnapshots ? ResolveSnapshots() : null;
            var crashDumpScan = CrashDumpLocator.FindDumpFiles();
            if (crashDumpScan.Files.Count == 0) missing.Report("crashDump", CrashDumpLocator.MissingReason(crashDumpScan));

            return PreviousSessionArtifacts.Unclean(request.LastSessionDirectory, recordingDirectory, snapshotsDirectory, playerLogPath, crashDumpScan.Files, salvagedProcessIds, exitedCleanlyByProcessId, previousOrigin, missing.Items);

            #region Internal

            // 新しい退避物があるときだけ退避先を空にする。無いまま空にすると、前世代の本物のクラッシュ資料が二度と提示されない
            // The destination is emptied only when new files arrive; emptying it regardless would drop an older real crash's evidence for good
            void MoveUncleanRecordings()
            {
                var hasNewRecording = false;
                foreach (var session in uncleanSessions) hasNewRecording |= session.RecordingDirectory != null;
                if (!hasNewRecording) return;

                PreviousSessionSalvage.ClearPreviousGeneration(recordingDestination, missing);
                foreach (var session in uncleanSessions)
                {
                    if (session.RecordingDirectory == null) continue;
                    var processDirectoryName = RecordingProcessDirectories.ProcessDirectoryPrefix + session.ProcessId;
                    var move = BugReportDiskOperations.MoveDirectory(session.RecordingDirectory, Path.Combine(recordingDestination, processDirectoryName, session.SessionName));
                    if (!move.Succeeded)
                    {
                        missing.Report(BugReportBundleLayout.RecordingDirectoryName, $"pid {session.ProcessId} {session.SessionName}: {move.FailureReason}");
                        continue;
                    }
                    if (!salvagedProcessIds.Contains(session.ProcessId)) salvagedProcessIds.Add(session.ProcessId);
                    PreviousSessionSalvage.DeleteEmptiedProcessDirectory(session, missing);
                }
            }

            // 起動先設定や前世代の退避物を、落ちたsessionの資料として代用しない
            // Neither this boot's settings nor older salvaged files substitute for the crashed session's evidence
            bool MoveWorldSnapshots()
            {
                var capture = previousOrigin?.SnapshotCapture;
                if (!SnapshotOwnershipMatches(capture?.Directory)) return false;
                var move = BugReportDiskOperations.MoveFilesInto(capture.Directory, snapshotDestination, out var moved);
                if (!move.Succeeded) missing.Report(BugReportBundleLayout.SnapshotDirectoryName, move.FailureReason);
                if (moved.Count == 0) return false;
                // 掃除後に移せた資料だけを返し、部分回収の所有も再提示へ残す
                // Return only files moved after clearing, and retain partial salvage ownership for replay
                previousOrigin.WriteTo(Path.Combine(snapshotDestination, WorldDataDirectory.SnapshotOwnerFileName));
                return true;
            }

            string ResolveSnapshots()
            {
                var probe = BugReportDiskOperations.ProbeHasAnyFile(snapshotDestination,
                    new[] { WorldDataDirectory.SnapshotFileSearchPattern, WorldDataDirectory.PacketLogFileSearchPattern });
                if (probe.Succeeded) return snapshotDestination;
                missing.Report(BugReportBundleLayout.SnapshotDirectoryName, $"所有印だけではsnapshot/packet資料にならない: {probe.FailureReason}");
                return null;
            }

            bool SnapshotOwnershipMatches(string directory)
            {
                var capture = previousOrigin?.SnapshotCapture;
                if (capture == null || capture.MissingReason != null)
                {
                    missing.Report(BugReportBundleLayout.SnapshotDirectoryName, capture?.MissingReason ?? "前回sessionの出所が読めずsnapshot所有を確認できない");
                    return false;
                }

                var source = SessionOriginSnapshot.ReadFrom(Path.Combine(directory, WorldDataDirectory.SnapshotOwnerFileName), out var reason);
                if (source == null || source.SnapshotCapture.MissingReason != null || source.SnapshotCapture.Owner != capture.Owner || source.SnapshotCapture.Directory != capture.Directory)
                {
                    missing.Report(BugReportBundleLayout.SnapshotDirectoryName, $"保存元の所有印が前回sessionと一致しない（別sessionによる再利用、印の欠落を含む）: {reason}");
                    return false;
                }

                return true;
            }

            // 複数のセッションが落ちていれば最新の出所を載せる。どれを載せたかは欠損列に残し、無音で1つへ潰さない（F12）
            // With several crashed sessions the newest origin is carried; which one is recorded in missing instead of silently collapsing to one (F12)
            SessionOriginSnapshot PersistLatestOrigin()
            {
                var latest = uncleanSessions[0];
                foreach (var session in uncleanSessions)
                    if (0 < ProcessSessionScope.CompareSessionNames(session.SessionName, latest.SessionName)) latest = session;
                if (1 < uncleanSessions.Count) missing.Report("previousOrigin", $"異常終了したセッションが{uncleanSessions.Count}件あり、最新の pid {latest.ProcessId} {latest.SessionName} の出所を載せた");

                if (latest.Origin != null)
                {
                    latest.Origin.WriteTo(originPath);
                    return latest.Origin;
                }

                missing.Report("previousOrigin", $"前回セッションの出所が不明（どのビルドで落ちたか分からない）: {latest.OriginMissingReason}");
                var deletion = BugReportFileOperations.DeleteFile(originPath);
                if (!deletion.Succeeded) missing.Report("previousOrigin", $"前世代の出所を消せなかった: {deletion.FailureReason}");
                return null;
            }

            SessionOriginSnapshot ReadPersistedOrigin()
            {
                var origin = SessionOriginSnapshot.ReadFrom(originPath, out var failureReason);
                if (origin == null) missing.Report("previousOrigin", $"再提示するクラッシュ資料の出所が不明: {failureReason}");
                return origin;
            }

            // 退避先に中身があれば、今回移した分でも前世代の持ち越しでも同じく提示する（次回起動で聞き直せるという約束を守る）
            // Whatever sits in the destination is presented, newly moved or carried over, keeping the promise that the next boot can ask again
            string ResolveSalvagedDirectory(string destination, string item)
            {
                var probe = BugReportDiskOperations.ProbeHasAnyFile(destination);
                if (probe.Succeeded) return destination;
                missing.Report(item, probe.FailureReason);
                return null;
            }

            #endregion
        }
    }
}
