using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Game.Paths;

namespace Client.Game.InGame.BugReport
{
    // 箱へ入れた台帳と、入れられず残した台帳の配置結果。索引の後始末はこの結果だけで決まる
    // What ended up in the bundle and what stayed behind; the index cleanup is decided from this alone
    internal readonly struct RemoteExecBundleLedgerPlacement
    {
        internal readonly string LastSessionDirectory;
        internal readonly IReadOnlyList<string> BundledLedgerFileNames;
        internal readonly IReadOnlyList<string> RetainedLedgerFileNames;

        // 索引そのものを読めたか。読めない索引を消すと、残った台帳の在処が永久に分からなくなる
        // Whether the index itself was readable; deleting an unreadable one would lose the location of every remaining ledger
        internal readonly bool IndexReadable;

        internal RemoteExecBundleLedgerPlacement(string lastSessionDirectory, IReadOnlyList<string> bundled, IReadOnlyList<string> retained, bool indexReadable)
        {
            LastSessionDirectory = lastSessionDirectory;
            BundledLedgerFileNames = bundled;
            RetainedLedgerFileNames = retained;
            IndexReadable = indexReadable;
        }
    }

    // bugとcrashの箱へ同じ契約で印と台帳を載せる
    // Attach the mark and ledgers to bug and crash bundles through the same contract
    internal static class RemoteExecBundleMark
    {
        internal static void ApplyForCurrentSession(BugReportManifest manifest, string bundleDirectory)
        {
            if (!RemoteExecLaunchOption.IsEnabled)
            {
                manifest.RemoteExec = RemoteExecMark.Disabled();
                return;
            }
            manifest.RemoteExec = RemoteExecMark.Enabled();
            if (RemoteExecLedger.HasWriteFailure)
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, "遠隔実行の台帳または実行試行の印を書けなかった");
            // 台帳パスの解決も外部ディスク境界。失敗理由を箱へ載せる
            // Resolving the ledger path is also a disk boundary; put failures in the bundle
            try { CopyLedger(manifest, bundleDirectory, RemoteExecLedger.CurrentPath, RemoteExecLedger.HasAttempted && !RemoteExecLedger.HasWriteFailure); }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e) || e is ArgumentException)
            {
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"遠隔実行の台帳パスを解決できなかった: {e.Message}");
            }
        }

        // 退避一覧が唯一の入口。落ちたセッションの有効状態はこの索引だけから決まる
        // The salvage index is the only entry point; a crashed session's enabled state is decided from it alone
        internal static RemoteExecBundleLedgerPlacement ApplyForSalvagedSessions(BugReportManifest manifest, string bundleDirectory, string lastSessionDirectory)
        {
            var bundled = new List<string>();
            var retained = new List<string>();
            if (!PreviousSessionRemoteExecLedgers.TryRead(lastSessionDirectory, out var entries, out var reason))
                return Unknown("遠隔実行台帳一覧が無く、前回セッションの遠隔実行の有効状態が不明", false);
            if (reason != null) return Unknown(reason, false);

            // 索引が空なのは「どのセッションも遠隔実行を使っていない」という読み取れた事実
            // An empty index is the readable fact that no session used remote execution
            if (entries.Count == 0)
            {
                manifest.RemoteExec = RemoteExecMark.Disabled();
                return new RemoteExecBundleLedgerPlacement(lastSessionDirectory, bundled, retained, true);
            }

            manifest.RemoteExec = RemoteExecMark.Enabled();
            foreach (var entry in entries)
            {
                // 一覧は検証済みのファイル名だけを含む。コピー失敗は項目ごとに欠損へ残す
                // The index contains validated file names; each failed copy declares its own gap
                if (entry.WriteFailed) manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"{entry.Name}: 遠隔実行の台帳または実行試行の印を書けなかった");
                if (CopyLedger(manifest, bundleDirectory, RemoteExecLedger.PathForFileName(entry.Name), entry.Attempted && !entry.WriteFailed)) bundled.Add(entry.Name);
                else retained.Add(entry.Name);
            }
            return new RemoteExecBundleLedgerPlacement(lastSessionDirectory, bundled, retained, true);

            #region Internal

            // 読めなかった索引は無効と名乗らず、理由つきの不明として箱とログの両方へ残す
            // An unreadable index never claims "disabled"; it becomes an unknown with its reason in both the bundle and the log
            RemoteExecBundleLedgerPlacement Unknown(string unknownReason, bool indexReadable)
            {
                manifest.RemoteExec = RemoteExecMark.Unknown(unknownReason);
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, unknownReason);
                return new RemoteExecBundleLedgerPlacement(lastSessionDirectory, bundled, retained, indexReadable);
            }

            #endregion
        }

        // 箱が台帳の写しを持てたときだけtrue。falseは元を残す必要がある
        // True only when the bundle holds the copy; false means the source must be kept
        private static bool CopyLedger(BugReportManifest manifest, string bundleDirectory, string source, bool attempted)
        {
            if (!File.Exists(source))
            {
                // 試行印が無ければ未実行。印があれば台帳書込失敗を欠損へ残す
                // No attempt signal means no run; a signal without a ledger declares a write gap
                if (attempted) manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"実行試行があったが遠隔実行の台帳が無い: {source}");
                return true;
            }

            // 台帳コピーはディスクIO境界。失敗時も印を残し欠損とログを出す
            // Ledger copying is a disk IO boundary; failures keep the mark and declare the gap in the log and manifest
            try
            {
                var directory = Path.Combine(bundleDirectory, BugReportBundleLayout.RemoteExecDirectoryName);
                Directory.CreateDirectory(directory);
                var name = Path.GetFileName(source);
                File.Copy(source, Path.Combine(directory, name), true);
                manifest.RemoteExec.LedgerFiles.Add($"{BugReportBundleLayout.RemoteExecDirectoryName}/{name}");
                return true;
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"遠隔実行の台帳をコピーできなかった: {e.Message}");
                return false;
            }
        }

        // READY箱へ写せた台帳だけ消し、写せなかった分と索引は再送元として残す
        // Remove only the ledgers a READY bundle contains, keeping the rest and the index as retry sources
        internal static void ReleaseBundledLedgers(RemoteExecBundleLedgerPlacement placement)
        {
            foreach (var name in placement.BundledLedgerFileNames)
            {
                var deletion = BugReportFileOperations.DeleteFile(RemoteExecLedger.PathForFileName(name));
                if (!deletion.Succeeded) UnityEngine.Debug.LogWarning($"遠隔実行の台帳を整理できませんでした: {deletion.FailureReason}");
            }

            // 索引は残した台帳が1本も無いときだけ消す。残したまま消すと台帳が恒久的に孤立する
            // The index is removed only when nothing was retained; removing it otherwise orphans those ledgers forever
            if (!placement.IndexReadable || placement.RetainedLedgerFileNames.Count != 0)
            {
                UnityEngine.Debug.LogWarning($"遠隔実行の台帳一覧を残します（写せなかった台帳 {placement.RetainedLedgerFileNames.Count} 本・索引読み取り {placement.IndexReadable}）");
                return;
            }
            var indexDeletion = BugReportFileOperations.DeleteFile(PreviousSessionRemoteExecLedgers.PathIn(placement.LastSessionDirectory));
            if (!indexDeletion.Succeeded) UnityEngine.Debug.LogWarning($"遠隔実行の台帳一覧を整理できませんでした: {indexDeletion.FailureReason}");
        }
    }
}
