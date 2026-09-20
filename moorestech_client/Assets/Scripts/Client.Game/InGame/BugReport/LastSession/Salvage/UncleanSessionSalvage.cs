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

            if (carriesPendingReport)
            {
                // 前回は正常終了だが、それより前のクラッシュ資料にまだ誰も答えていない。移動も上書きもせず前世代をそのまま提示し直す（F04）
                // The last exit was clean, yet nobody answered an older crash's evidence; nothing is moved or overwritten and that generation is presented again (F04)
                Debug.Log("前回異常終了の確認が未応答のため、last-session に残る前世代の資料を再提示します");
                previousOrigin = ReadPersistedOrigin();
                playerLogPath = null;
                missing.Report("playerLog", "未応答のクラッシュ資料を再提示する起動のため、その後の正常終了セッションが Player-prev.log を上書き済み");
            }
            else
            {
                // 退避元は前回セッション自身の印から決める。今回の起動設定で代用すると、別ワールドのスナップショットが異常終了箱へ混入する（D-C3）
                // The source is decided from the previous session's own mark; standing in with this boot's settings would mix another world's snapshots into the crash box (D-C3)
                var latest = SelectLatestUncleanSession();
                MoveUncleanRecordings();
                MoveWorldSnapshots(latest.Origin);
                previousOrigin = PersistOrigin(latest);
                playerLogPath = PlayerLogLocator.PreviousSessionLogPath();
                if (playerLogPath == null) missing.Report("playerLog", "前回セッションのPlayer-prev.logが見つからない");
            }

            var recordingDirectory = ResolveSalvagedDirectory(recordingDestination, BugReportBundleLayout.RecordingDirectoryName);
            var snapshotsDirectory = ResolveSalvagedDirectory(snapshotDestination, BugReportBundleLayout.SnapshotDirectoryName);
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

            // リモート接続にはスナップショットを書く内蔵サーバーがそもそも居ない。退避失敗と同じ理由文にすると毎回「失敗」に見える
            // A remote connection has no embedded server writing snapshots at all; sharing the failure wording would read as a failure every time
            void MoveWorldSnapshots(SessionOriginSnapshot origin)
            {
                // 退避元を記録しない旧版の印。既定ワールドで代用せず、源が分からないことをそのまま表明する（D-C3）
                // An older mark that recorded no source; rather than standing in with the default world, the unknown source is declared as it is (D-C3)
                if (origin?.SnapshotSource == null)
                {
                    missing.Report(BugReportBundleLayout.SnapshotDirectoryName, "スナップショット源不明: 前回セッションの印に退避元（接続種別・ワールド）の記録が無い");
                    return;
                }

                if (origin.SnapshotSource.IsRemoteConnection)
                {
                    missing.Report(BugReportBundleLayout.SnapshotDirectoryName, "リモート接続のセッションのため内蔵サーバーのスナップショットは存在しない");
                    return;
                }

                var move = BugReportDiskOperations.MoveFilesInto(origin.SnapshotSource.WorldSnapshotDirectory, snapshotDestination);
                if (!move.Succeeded) missing.Report(BugReportBundleLayout.SnapshotDirectoryName, move.FailureReason);
            }

            // 複数のセッションが落ちていれば最新のものを採る。どれを採ったかは欠損列に残し、無音で1つへ潰さない（F12）
            // With several crashed sessions the newest one is taken; which one is recorded in missing instead of silently collapsing to one (F12)
            PreviousProcessSession SelectLatestUncleanSession()
            {
                var newest = uncleanSessions[0];
                foreach (var session in uncleanSessions)
                    if (0 < ProcessSessionScope.CompareSessionNames(session.SessionName, newest.SessionName)) newest = session;
                if (1 < uncleanSessions.Count) missing.Report("previousOrigin", $"異常終了したセッションが{uncleanSessions.Count}件あり、最新の pid {newest.ProcessId} {newest.SessionName} の出所を載せた");
                return newest;
            }

            SessionOriginSnapshot PersistOrigin(PreviousProcessSession latest)
            {
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
