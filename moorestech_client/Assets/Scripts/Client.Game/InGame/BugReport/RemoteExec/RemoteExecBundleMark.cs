using System;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
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
            manifest.RemoteExec = new RemoteExecMark { Enabled = true };
            CopyLedger(manifest, bundleDirectory, RemoteExecLedger.PathFor(RecordingProcessDirectories.CurrentProcessId()), false);
        }

        // 今回の起動設定でなく、落ちたセッション自身が書いた印を使う
        // Use the crashed session's own mark rather than the current launch setting
        internal static void ApplyForPreviousSession(BugReportManifest manifest, string bundleDirectory, SessionOriginSnapshot previousOrigin)
        {
            if (previousOrigin == null || !previousOrigin.RemoteExecEnabled) return;
            manifest.RemoteExec = new RemoteExecMark { Enabled = true };
            if (previousOrigin.RemoteExecLedgerFileName == null)
            {
                manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, "前回セッションの出所に遠隔実行の台帳ファイル名が無い");
                return;
            }
            CopyLedger(manifest, bundleDirectory, Path.Combine(RemoteExecAccessFile.DirectoryPath, previousOrigin.RemoteExecLedgerFileName), true);
        }

        private static void CopyLedger(BugReportManifest manifest, string bundleDirectory, string source, bool missingIfAbsent)
        {
            if (!File.Exists(source))
            {
                // 前回有効なら台帳なしを欠損として申告する。今回の未実行だけは正常
                // A prior enabled session missing its ledger is declared missing; a current unexecuted session is valid
                if (missingIfAbsent) manifest.AddMissing(BugReportBundleLayout.RemoteExecDirectoryName, $"遠隔実行の台帳が無い、または読めない: {source}");
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
    }
}
