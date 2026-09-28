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
        public void 壊れた前世代の台帳一覧は欠損を表明して新しい一覧を書き直す()
        {
            var root = Path.Combine(Path.GetTempPath(), "crash-remote-exec-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                PendingCrashReportMark.MarkPending(root);
                File.WriteAllText(PreviousSessionRemoteExecLedgers.PathIn(root), "{broken");
                var name = RemoteExecLedgerWriter.FileNameFor(654324, "session_400");
                var artifacts = PreviousSessionSalvage.Salvage(new PreviousSessionSalvageRequest
                {
                    LastSessionDirectory = root,
                    PreviousSessions = new List<PreviousProcessSession>
                    {
                        new PreviousProcessSession
                        {
                            ProcessId = 654324,
                            SessionName = "session_400",
                            Origin = new SessionOriginSnapshot(null, TestPreviousSessionArtifacts.OriginSteamIdAbsenceReason,
                                BuildOriginReading.Editor(), new RemoteExecOriginMark(name)),
                        },
                    },
                });
                Assert.IsTrue(artifacts.Missing.Any(item => item.Item == BugReportBundleLayout.RemoteExecDirectoryName &&
                    item.Reason.Contains("前世代の遠隔実行台帳一覧を引き継げなかった")));
                Assert.IsTrue(PreviousSessionRemoteExecLedgers.TryRead(root, out var entries, out var reason));
                Assert.IsNull(reason);
                Assert.AreEqual(name, entries.Single().Name);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void 連続する複数クラッシュの台帳を未応答起動後の箱へ全て入れREADY後に削除する()
        {
            var root = Path.Combine(Path.GetTempPath(), "crash-remote-exec-" + Guid.NewGuid().ToString("N"));
            var names = new[]
            {
                RemoteExecLedgerWriter.FileNameFor(654321, "session_100"),
                RemoteExecLedgerWriter.FileNameFor(654322, "session_200"),
            };
            var paths = names.Select(RemoteExecLedger.PathForFileName).ToArray();
            var originals = paths.Select(path => File.Exists(path) ? File.ReadAllBytes(path) : null).ToArray();
            var directoryExisted = Directory.Exists(RemoteExecAccessFile.DirectoryPath);
            string bundle = null;

            try
            {
                Directory.CreateDirectory(RemoteExecAccessFile.DirectoryPath);
                File.WriteAllText(paths[0], "older ledger");
                File.WriteAllText(paths[1], "middle ledger");
                var sessions = new List<PreviousProcessSession>();
                for (var i = 0; i < names.Length; i++) sessions.Add(new PreviousProcessSession
                {
                    ProcessId = 654321 + i,
                    SessionName = "session_" + (100 * (i + 1)),
                    Origin = new SessionOriginSnapshot(null, TestPreviousSessionArtifacts.OriginSteamIdAbsenceReason,
                        BuildOriginReading.Editor(), new RemoteExecOriginMark(names[i])),
                });
                sessions.Add(new PreviousProcessSession
                {
                    ProcessId = 654323,
                    SessionName = "session_300",
                    Origin = new SessionOriginSnapshot(null, TestPreviousSessionArtifacts.OriginSteamIdAbsenceReason,
                        BuildOriginReading.Editor(), null),
                });

                // 別々の起動で検知したクラッシュを統合し、最新が遠隔実行なしでも古い台帳を保持する
                // Merge crashes detected on separate launches and keep older ledgers when the newest session disabled remote execution
                var lastSession = Path.Combine(root, "last-session");
                PreviousSessionSalvage.Salvage(new PreviousSessionSalvageRequest
                {
                    LastSessionDirectory = lastSession,
                    PreviousSessions = new List<PreviousProcessSession> { sessions[0] },
                });
                PreviousSessionSalvage.Salvage(new PreviousSessionSalvageRequest
                {
                    LastSessionDirectory = lastSession,
                    PreviousSessions = new List<PreviousProcessSession> { sessions[1], sessions[2] },
                });
                var carried = PreviousSessionSalvage.Salvage(new PreviousSessionSalvageRequest
                {
                    LastSessionDirectory = lastSession,
                });
                bundle = CrashBundleWriter.Write(carried, "複数の遠隔実行", RepositoryStateProbe.RepositoryRoot,
                    RepositoryStateProbe.MasterDataRoot);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                Assert.AreEqual("Enabled", (string)manifest["remoteExec"]["state"]);
                foreach (var name in names)
                {
                    var relative = $"{BugReportBundleLayout.RemoteExecDirectoryName}/{name}";
                    CollectionAssert.Contains(manifest["remoteExec"]["ledgerFiles"].Select(item => (string)item).ToArray(), relative);
                    Assert.IsTrue(File.Exists(Path.Combine(bundle, relative)));
                    Assert.IsFalse(File.Exists(RemoteExecLedger.PathForFileName(name)), "READY箱へ写した台帳が残っている");
                }
            }
            finally
            {
                if (bundle != null && Directory.Exists(bundle)) Directory.Delete(bundle, true);
                if (Directory.Exists(root)) Directory.Delete(root, true);
                for (var i = 0; i < paths.Length; i++)
                {
                    if (originals[i] == null)
                    {
                        if (File.Exists(paths[i])) File.Delete(paths[i]);
                    }
                    else File.WriteAllBytes(paths[i], originals[i]);
                }
                if (!directoryExisted && Directory.Exists(RemoteExecAccessFile.DirectoryPath) &&
                    Directory.GetFileSystemEntries(RemoteExecAccessFile.DirectoryPath).Length == 0)
                    Directory.Delete(RemoteExecAccessFile.DirectoryPath);
            }
        }

        // 台帳一覧が無い退避は「無効」と名乗らず、不明として理由つきで表明する
        // A salvage without the ledger index never claims "disabled"; it declares unknown with its reason
        [Test]
        public void 台帳一覧の無い退避は不明として表明し台帳を載せない()
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
                    BuildOriginReading.Editor(), new RemoteExecOriginMark(ledgerFileName));
                var artifacts = PreviousSessionArtifacts.Unclean(TestPreviousSessionArtifacts.UnusedLastSessionDirectory,
                    null, null, null, new List<string>(), new List<int>(), new Dictionary<int, bool>(),
                    origin, new List<MissingItem>());
                bundle = CrashBundleWriter.Write(artifacts, "遠隔実行あり", RepositoryStateProbe.RepositoryRoot,
                    RepositoryStateProbe.MasterDataRoot);

                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                Assert.AreEqual("Unknown", (string)manifest["remoteExec"]["state"]);
                StringAssert.Contains("不明", (string)manifest["remoteExec"]["unknownReason"]);
                Assert.IsEmpty(manifest["remoteExec"]["ledgerFiles"].Select(item => (string)item).ToList());
                Assert.IsTrue(manifest["missing"].Any(item => (string)item["item"] == BugReportBundleLayout.RemoteExecDirectoryName));
                Assert.IsTrue(File.Exists(ledgerPath), "在処の分からない台帳を消してはならない");
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
