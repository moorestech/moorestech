using System;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Game.Paths;

namespace Client.Game.InGame.BugReport
{
    // bugとcrashの箱へ同じ契約で印と台帳を載せる
    // Attach the mark and ledgers to bug and crash bundles through the same contract
    internal static class RemoteExecBundleMark
    {
        internal static void ApplyForCurrentSession(BugReportManifest manifest, string bundleDirectory)
        {
            if (!RemoteExecLaunchOption.IsEnabled) return;
            manifest.RemoteExec = new RemoteExecMark();
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

        // 今回の起動設定でなく、落ちたセッション自身が書いた印を使う
        // Use the crashed session's own mark rather than the current launch setting
        internal static void ApplyForPreviousSession(BugReportManifest manifest, string bundleDirectory, SessionOriginSnapshot previousOrigin)
        {
            if (previousOrigin == null)
            {
                manifest.AddMissing("remoteExec", "前回セッションの出所が読めず、遠隔実行の有効状態が不明");
                return;
            }
            if (!previousOrigin.RemoteExecEnabled) return;
            manifest.RemoteExec = new RemoteExecMark();
            if (previousOrigin.RemoteExecLedgerWriteFailed)
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, "前回セッションで遠隔実行の台帳または実行試行の印を書けなかった");
            try
            {
                CopyLedger(manifest, bundleDirectory, RemoteExecLedger.PathForFileName(previousOrigin.RemoteExecLedgerFileName),
                    previousOrigin.RemoteExecAttempted && !previousOrigin.RemoteExecLedgerWriteFailed);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e) || e is ArgumentException)
            {
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"前回の台帳パスを解決できなかった: {e.Message}");
            }
        }

        private static void CopyLedger(BugReportManifest manifest, string bundleDirectory, string source, bool attempted)
        {
            if (!File.Exists(source))
            {
                // 試行印が無ければ未実行。印があれば台帳書込失敗を欠損へ残す
                // No attempt signal means no run; a signal without a ledger declares a write gap
                if (attempted) manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"実行試行があったが遠隔実行の台帳が無い: {source}");
                return;
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
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"遠隔実行の台帳をコピーできなかった: {e.Message}");
            }
        }

        // READY箱へ台帳を写せた前回分だけ消し、失敗時の再送元は残す
        // Remove a prior ledger only after a READY bundle contains its copy, retaining retry sources on failure
        internal static void ReleaseBundledPreviousLedger(BugReportManifest manifest, SessionOriginSnapshot previousOrigin)
        {
            if (previousOrigin?.RemoteExecLedgerFileName == null || manifest.RemoteExec == null || manifest.RemoteExec.LedgerFiles.Count == 0) return;
            var path = RemoteExecLedger.PathForFileName(previousOrigin.RemoteExecLedgerFileName);
            var deletion = BugReportFileOperations.DeleteFile(path);
            if (!deletion.Succeeded) UnityEngine.Debug.LogWarning($"遠隔実行の台帳を整理できませんでした: {deletion.FailureReason}");
        }
    }
}
