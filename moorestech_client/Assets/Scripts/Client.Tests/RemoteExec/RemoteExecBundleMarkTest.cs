using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public sealed class RemoteExecBundleMarkTest
    {
        private string _bundle;
        private RemoteExecTestFiles _files;
        private bool _wasEnabled;
        private string _previousLedger;
        private string _lastSession;

        [SetUp]
        public void SetUp()
        {
            // 実データを退避し、設定もテスト前の状態へ戻せるよう残す
            // Preserve real files and retain the launch setting for restoration
            _wasEnabled = RemoteExecLaunchOption.IsEnabled;
            RemoteExecLedger.Initialize("session_" + DateTime.UtcNow.Ticks, null);
            _files = new RemoteExecTestFiles();
            _bundle = Path.Combine(Path.GetTempPath(), "remote-exec-bundle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_bundle);
            _lastSession = Path.Combine(_bundle, "last-session");
        }

        [TearDown]
        public void TearDown()
        {
            if (_previousLedger != null && File.Exists(_previousLedger)) File.Delete(_previousLedger);
            _previousLedger = null;
            _files.Restore();
            RemoteExecLaunchOption.ResolveFromCommandLine(_wasEnabled ? new[] { RemoteExecLaunchOption.Marker } : Array.Empty<string>());
            Directory.Delete(_bundle, true);
        }

        [Test]
        public void 無効時は印も台帳ディレクトリも作らない()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
            WriteLedger();
            var manifest = new BugReportManifest();
            RemoteExecBundleMark.ApplyForCurrentSession(manifest, _bundle);
            Assert.AreEqual(RemoteExecManifestMarkState.Disabled, manifest.RemoteExec.State);
            Assert.IsFalse(Directory.Exists(Path.Combine(_bundle, BugReportBundleLayout.RemoteExecDirectoryName)));
            Assert.AreEqual("Disabled", (string)JObject.Parse(manifest.ToJson())["remoteExec"]["state"]);
        }

        [Test]
        public void 有効時は台帳の内容と相対パスを箱に載せる()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            var source = WriteLedger();
            var manifest = new BugReportManifest();
            RemoteExecBundleMark.ApplyForCurrentSession(manifest, _bundle);
            AssertCopiedLedger(manifest, source);
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual(5, (int)json["schemaVersion"]);
            Assert.AreEqual("Enabled", (string)json["remoteExec"]["state"]);
            Assert.AreEqual(JTokenType.Null, json["remoteExec"]["unknownReason"].Type);
        }

        [Test]
        public void 有効でも未実行なら欠損を付けない()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            var manifest = new BugReportManifest();
            RemoteExecBundleMark.ApplyForCurrentSession(manifest, _bundle);
            Assert.AreEqual(RemoteExecManifestMarkState.Enabled, manifest.RemoteExec.State);
            Assert.IsEmpty(manifest.RemoteExec.LedgerFiles);
            Assert.IsEmpty(manifest.Missing);
        }

        [Test]
        public void 退避一覧の台帳を箱に載せ写せた分だけ元を消す()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
            // 現在のpidを誤採用しても通らないよう、未使用の別pidに前回の台帳を置く
            // Use an unused different pid so accidentally selecting the current ledger cannot pass
            var previousId = 1000000000;
            while (File.Exists(PreviousLedgerPath(previousId)) || Directory.Exists(PreviousLedgerPath(previousId))) previousId++;
            _previousLedger = PreviousLedgerPath(previousId);
            Directory.CreateDirectory(RemoteExecAccessFile.DirectoryPath);
            File.WriteAllText(_previousLedger, "previous session ledger\n");
            WriteLedgerIndex(new JObject { ["name"] = Path.GetFileName(_previousLedger), ["attempted"] = true, ["writeFailed"] = false });

            var manifest = new BugReportManifest();
            var placement = RemoteExecBundleMark.ApplyForSalvagedSessions(manifest, _bundle, _lastSession);
            AssertCopiedLedger(manifest, _previousLedger);
            RemoteExecBundleMark.ReleaseBundledLedgers(placement);
            Assert.IsFalse(File.Exists(_previousLedger), "箱へ写した台帳が残っている");
            Assert.IsFalse(File.Exists(PreviousSessionRemoteExecLedgers.PathIn(_lastSession)), "残す台帳が無いのに索引が残っている");
        }

        [Test]
        public void 退避一覧が空なら無効として印を付ける()
        {
            var manifest = new BugReportManifest();
            WriteLedgerIndex();
            RemoteExecBundleMark.ApplyForSalvagedSessions(manifest, _bundle, _lastSession);
            Assert.AreEqual(RemoteExecManifestMarkState.Disabled, manifest.RemoteExec.State);
            Assert.IsEmpty(manifest.Missing);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 前回に実行試行があり台帳が無ければ欠損を付ける(bool failureSignal)
        {
            var manifest = new BugReportManifest();
            var missingId = 999999998;
            while (File.Exists(PreviousLedgerPath(missingId))) missingId--;
            WriteLedgerIndex(new JObject
            {
                ["name"] = Path.GetFileName(PreviousLedgerPath(missingId)),
                ["attempted"] = true,
                ["writeFailed"] = failureSignal,
            });
            LogAssert.Expect(LogType.Warning, new Regex(failureSignal ? "遠隔実行の台帳または実行試行の印を書けなかった" : "実行試行があったが遠隔実行の台帳が無い"));
            RemoteExecBundleMark.ApplyForSalvagedSessions(manifest, _bundle, _lastSession);
            Assert.AreEqual(RemoteExecManifestMarkState.Enabled, manifest.RemoteExec.State);
            Assert.IsEmpty(manifest.RemoteExec.LedgerFiles);
            Assert.AreEqual(1, manifest.Missing.Count);
            Assert.AreEqual(BugReportBundleLayout.RemoteExecDirectoryName, manifest.Missing[0].Item);
        }

        // 索引が壊れていれば不明として表明し、索引も台帳も消さない（消すと在処が永久に分からなくなる）
        // A corrupt index declares unknown and removes neither the index nor the ledgers, whose location would otherwise be lost forever
        [Test]
        public void 壊れた退避一覧は不明として表明し索引を残す()
        {
            var manifest = new BugReportManifest();
            Directory.CreateDirectory(_lastSession);
            File.WriteAllText(PreviousSessionRemoteExecLedgers.PathIn(_lastSession), "{broken");
            LogAssert.Expect(LogType.Warning, new Regex("遠隔実行台帳一覧を読めなかった"));
            LogAssert.Expect(LogType.Warning, new Regex("遠隔実行の台帳一覧を残します"));
            var placement = RemoteExecBundleMark.ApplyForSalvagedSessions(manifest, _bundle, _lastSession);
            Assert.AreEqual(RemoteExecManifestMarkState.Unknown, manifest.RemoteExec.State);
            StringAssert.Contains("遠隔実行台帳一覧を読めなかった", manifest.RemoteExec.UnknownReason);
            RemoteExecBundleMark.ReleaseBundledLedgers(placement);
            Assert.IsTrue(File.Exists(PreviousSessionRemoteExecLedgers.PathIn(_lastSession)));
        }

        [Test]
        public void コピー失敗でも有効印を保ち理由をログと欠損に残す()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            WriteLedger();
            File.WriteAllText(Path.Combine(_bundle, BugReportBundleLayout.RemoteExecDirectoryName), "blocking file");
            var manifest = new BugReportManifest();
            LogAssert.Expect(LogType.Warning, new Regex("遠隔実行の台帳をコピーできなかった"));
            RemoteExecBundleMark.ApplyForCurrentSession(manifest, _bundle);
            Assert.AreEqual(RemoteExecManifestMarkState.Enabled, manifest.RemoteExec.State);
            Assert.IsEmpty(manifest.RemoteExec.LedgerFiles);
            Assert.AreEqual(BugReportBundleLayout.RemoteExecDirectoryName, manifest.Missing[0].Item);
        }

        private static string WriteLedger()
        {
            Directory.CreateDirectory(RemoteExecAccessFile.DirectoryPath);
            var path = RemoteExecLedger.CurrentPath;
            File.WriteAllText(path, "{\"code\":\"return 1;\",\"ok\":true}\n");
            return path;
        }

        private static string PreviousLedgerPath(int processId)
        {
            return RemoteExecLedger.PathForFileName(RemoteExecLedgerWriter.FileNameFor(processId, "session_123"));
        }

        private void WriteLedgerIndex(params JObject[] entries)
        {
            Directory.CreateDirectory(_lastSession);
            File.WriteAllText(PreviousSessionRemoteExecLedgers.PathIn(_lastSession),
                new JObject { ["ledgers"] = new JArray(entries) }.ToString());
        }

        private void AssertCopiedLedger(BugReportManifest manifest, string source)
        {
            var relative = BugReportBundleLayout.RemoteExecDirectoryName + "/" + Path.GetFileName(source);
            Assert.AreEqual(RemoteExecManifestMarkState.Enabled, manifest.RemoteExec.State);
            CollectionAssert.AreEqual(new[] { relative }, manifest.RemoteExec.LedgerFiles);
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(Path.Combine(_bundle, relative)));
            Assert.IsEmpty(manifest.Missing);
        }
    }
}
