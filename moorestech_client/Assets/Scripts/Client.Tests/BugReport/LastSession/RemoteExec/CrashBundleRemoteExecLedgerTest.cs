using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.RemoteExec.Access;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 異常終了の台帳がREADY箱に入り、元の台帳を片付ける契約を固定する
    // Pin the contract that a crash ledger enters a READY bundle before its source is removed
    public sealed class CrashBundleRemoteExecLedgerTest
    {
        [Test]
        public void 遠隔実行が有効だった前回セッションの印と台帳が箱へ入る()
        {
            var previousProcessId = Process.GetCurrentProcess().Id + 1;
            var ledgerFileName = RemoteExecLedgerWriter.FileNameFor(previousProcessId, "session_123");
            var ledgerPath = RemoteExecLedger.PathForFileName(ledgerFileName);
            var directoryExisted = Directory.Exists(RemoteExecAccessFile.DirectoryPath);
            var originalLedger = File.Exists(ledgerPath) ? File.ReadAllBytes(ledgerPath) : null;
            string bundle = null;

            // 実ユーザーデータを保全し、異なるPIDの前回台帳を模擬する
            // Preserve real user data while simulating a previous ledger from another PID
            try
            {
                Directory.CreateDirectory(RemoteExecAccessFile.DirectoryPath);
                File.WriteAllText(ledgerPath, "{\"event\":\"start\"}\n");
                var origin = new SessionOriginSnapshot(null, TestPreviousSessionArtifacts.OriginSteamIdAbsenceReason,
                    BuildOriginReading.Editor(), ledgerFileName);
                var artifacts = PreviousSessionArtifacts.Unclean(TestPreviousSessionArtifacts.UnusedLastSessionDirectory,
                    null, null, null, new List<string>(), new List<int>(), new Dictionary<int, bool>(),
                    origin, new List<MissingItem>());
                bundle = CrashBundleWriter.Write(artifacts, "遠隔実行あり", RepositoryStateProbe.RepositoryRoot,
                    RepositoryStateProbe.MasterDataRoot);

                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                Assert.IsNotNull(manifest["remoteExec"]);
                var relativePath = BugReportBundleLayout.RemoteExecDirectoryName + "/" + ledgerFileName;
                CollectionAssert.Contains(manifest["remoteExec"]["ledgerFiles"].Select(item => (string)item).ToList(), relativePath);
                Assert.AreEqual("{\"event\":\"start\"}\n", File.ReadAllText(Path.Combine(bundle, relativePath)));
                Assert.IsFalse(File.Exists(ledgerPath), "READY箱へ写した台帳が残っている");
            }
            finally
            {
                if (bundle != null && Directory.Exists(bundle)) Directory.Delete(bundle, true);
                if (originalLedger == null)
                {
                    if (File.Exists(ledgerPath)) File.Delete(ledgerPath);
                }
                else File.WriteAllBytes(ledgerPath, originalLedger);
                if (!directoryExisted && Directory.Exists(RemoteExecAccessFile.DirectoryPath) &&
                    Directory.GetFileSystemEntries(RemoteExecAccessFile.DirectoryPath).Length == 0)
                    Directory.Delete(RemoteExecAccessFile.DirectoryPath);
            }
        }
    }
}
