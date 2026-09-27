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

        [SetUp]
        public void SetUp()
        {
            // 実データを退避し、設定もテスト前の状態へ戻せるよう残す
            // Preserve real files and retain the launch setting for restoration
            _wasEnabled = RemoteExecLaunchOption.IsEnabled;
            _files = new RemoteExecTestFiles();
            _bundle = Path.Combine(Path.GetTempPath(), "remote-exec-bundle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_bundle);
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
            Assert.IsNull(manifest.RemoteExec);
            Assert.IsFalse(Directory.Exists(Path.Combine(_bundle, BugReportBundleLayout.RemoteExecDirectoryName)));
            Assert.AreEqual(JTokenType.Null, JObject.Parse(manifest.ToJson())["remoteExec"].Type);
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
            Assert.AreEqual(4, (int)json["schemaVersion"]);
            Assert.IsTrue((bool)json["remoteExec"]["enabled"]);
        }

        [Test]
        public void 有効でも未実行なら欠損を付けない()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            var manifest = new BugReportManifest();
            RemoteExecBundleMark.ApplyForCurrentSession(manifest, _bundle);
            Assert.IsTrue(manifest.RemoteExec.Enabled);
            Assert.IsEmpty(manifest.RemoteExec.LedgerFiles);
            Assert.IsEmpty(manifest.Missing);
        }

        [Test]
        public void 録画が無くても前回の出所が記録した台帳を載せる()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
            WriteLedger();
            // 現在のpidを誤採用しても通らないよう、未使用の別pidに前回の台帳を置く
            // Use an unused different pid so accidentally selecting the current ledger cannot pass
            var previousId = 1000000000;
            while (File.Exists(RemoteExecLedger.PathFor(previousId)) || Directory.Exists(RemoteExecLedger.PathFor(previousId))) previousId++;
            _previousLedger = RemoteExecLedger.PathFor(previousId);
            File.WriteAllText(_previousLedger, "previous session ledger\n");
            var manifest = new BugReportManifest();
            var origin = PreviousOrigin(Path.GetFileName(_previousLedger));
            var originPath = Path.Combine(_bundle, "previous-origin.json");
            Assert.IsTrue(origin.WriteTo(originPath).Succeeded);
            origin = SessionOriginSnapshot.ReadFrom(originPath, out var reason);
            Assert.IsNull(reason);
            RemoteExecBundleMark.ApplyForPreviousSession(manifest, _bundle, origin);
            AssertCopiedLedger(manifest, _previousLedger);
        }

        [Test]
        public void 前回有効なのに台帳が無ければ理由を欠損とログへ残す()
        {
            var manifest = new BugReportManifest();
            var missingId = 999999999;
            while (File.Exists(RemoteExecLedger.PathFor(missingId))) missingId++;
            var missingName = Path.GetFileName(RemoteExecLedger.PathFor(missingId));
            LogAssert.Expect(LogType.Warning, new Regex("遠隔実行の台帳が無い"));
            RemoteExecBundleMark.ApplyForPreviousSession(manifest, _bundle, PreviousOrigin(missingName));
            Assert.IsTrue(manifest.RemoteExec.Enabled);
            Assert.IsEmpty(manifest.RemoteExec.LedgerFiles);
            StringAssert.Contains("台帳が無い", manifest.Missing[0].Reason);
        }

        [Test]
        public void 前回無効なら今回有効でも印を付けない()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            WriteLedger();
            var manifest = new BugReportManifest();
            RemoteExecBundleMark.ApplyForPreviousSession(manifest, _bundle, PreviousOrigin(null));
            Assert.IsNull(manifest.RemoteExec);
            Assert.IsFalse(Directory.Exists(Path.Combine(_bundle, BugReportBundleLayout.RemoteExecDirectoryName)));
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
            Assert.IsTrue(manifest.RemoteExec.Enabled);
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

        private static SessionOriginSnapshot PreviousOrigin(string ledgerFileName)
        {
            return new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), ledgerFileName,
                SessionSnapshotCapture.NotStarted(), new List<MissingItem>());
        }

        private void AssertCopiedLedger(BugReportManifest manifest, string source)
        {
            var relative = BugReportBundleLayout.RemoteExecDirectoryName + "/" + Path.GetFileName(source);
            Assert.IsTrue(manifest.RemoteExec.Enabled);
            CollectionAssert.AreEqual(new[] { relative }, manifest.RemoteExec.LedgerFiles);
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(Path.Combine(_bundle, relative)));
            Assert.IsEmpty(manifest.Missing);
        }
    }
}
