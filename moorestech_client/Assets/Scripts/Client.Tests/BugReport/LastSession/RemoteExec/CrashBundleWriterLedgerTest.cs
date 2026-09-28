using System;
using System.Collections.Generic;
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
    // 前回セッションの台帳が実際のcrash箱へ配線されることを検証する
    // Verify that the previous session's ledger reaches the actual crash bundle
    public class CrashBundleWriterLedgerTest
    {
        [Test]
        public void 前回セッションの台帳をcrash箱へ写して印を残す()
        {
            var ledgerName = RemoteExecLedgerWriter.FileNameFor(654321, "session_" + DateTime.UtcNow.Ticks);
            var ledgerPath = RemoteExecLedger.PathForFileName(ledgerName);
            var directoryExisted = Directory.Exists(RemoteExecAccessFile.DirectoryPath);
            var original = File.Exists(ledgerPath) ? File.ReadAllBytes(ledgerPath) : null;
            string bundle = null;

            try
            {
                // 実データを退避したうえで固有の台帳を置き、前回セッションの出所から箱を作る
                // Preserve real data before placing a unique ledger and building from the previous origin
                Directory.CreateDirectory(RemoteExecAccessFile.DirectoryPath);
                File.WriteAllText(ledgerPath, "previous-session-ledger");
                var origin = new SessionOriginSnapshot(null, TestPreviousSessionArtifacts.OriginSteamIdAbsenceReason,
                    BuildOriginReading.Editor(), ledgerName);
                var artifacts = PreviousSessionArtifacts.Unclean(TestPreviousSessionArtifacts.UnusedLastSessionDirectory,
                    null, null, null, new List<string>(), new List<int>(), new Dictionary<int, bool>(),
                    origin, new List<MissingItem>());
                bundle = CrashBundleWriter.Write(artifacts, "台帳配線", RepositoryStateProbe.RepositoryRoot,
                    RepositoryStateProbe.MasterDataRoot);

                Assert.IsNotNull(bundle);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                var relativePath = $"{BugReportBundleLayout.RemoteExecDirectoryName}/{ledgerName}";
                CollectionAssert.Contains(((JArray)manifest["remoteExec"]["ledgerFiles"]).Select(x => (string)x).ToArray(), relativePath);
                Assert.AreEqual("previous-session-ledger", File.ReadAllText(Path.Combine(bundle,
                    BugReportBundleLayout.RemoteExecDirectoryName, ledgerName)));
            }
            finally
            {
                if (bundle != null && Directory.Exists(bundle)) Directory.Delete(bundle, true);
                if (original == null)
                {
                    if (File.Exists(ledgerPath)) File.Delete(ledgerPath);
                }
                else File.WriteAllBytes(ledgerPath, original);
                if (!directoryExisted && Directory.Exists(RemoteExecAccessFile.DirectoryPath) &&
                    Directory.GetFileSystemEntries(RemoteExecAccessFile.DirectoryPath).Length == 0)
                    Directory.Delete(RemoteExecAccessFile.DirectoryPath);
            }
        }
    }
}
