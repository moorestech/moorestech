using System;
using System.Collections.Generic;
using System.IO;
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
            Apply(manifest, bundleDirectory, new[] { RecordingProcessDirectories.CurrentProcessId() });
        }

        // 今回の起動設定でなく、落ちたセッション自身が書いた印を使う
        // Use the crashed session's own mark rather than the current launch setting
        internal static void ApplyForPreviousSession(BugReportManifest manifest, string bundleDirectory, bool previousSessionEnabled, IReadOnlyList<int> salvagedProcessIds)
        {
            if (!previousSessionEnabled) return;
            Apply(manifest, bundleDirectory, salvagedProcessIds);
        }

        private static void Apply(BugReportManifest manifest, string bundleDirectory, IReadOnlyList<int> processIds)
        {
            manifest.RemoteExec = new RemoteExecMark { Enabled = true };
            foreach (var processId in new HashSet<int>(processIds))
            {
                var source = RemoteExecLedger.PathFor(processId);
                // 有効でも未実行なら台帳は無い。これは欠損ではない
                // An enabled session with no execution has no ledger; this is not missing evidence
                if (!File.Exists(source)) continue;

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
}
