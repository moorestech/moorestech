using System;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
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
        public void 前回有効なら今回無効でも退避プロセスの台帳を載せる()
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
            RemoteExecBundleMark.ApplyForPreviousSession(manifest, _bundle, true, new[] { previousId });
            AssertCopiedLedger(manifest, _previousLedger);
        }

        [Test]
        public void 前回無効なら今回有効でも印を付けない()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            WriteLedger();
            var manifest = new BugReportManifest();
            RemoteExecBundleMark.ApplyForPreviousSession(manifest, _bundle, false, new[] { RecordingProcessDirectories.CurrentProcessId() });
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
            var path = RemoteExecLedger.PathFor(RecordingProcessDirectories.CurrentProcessId());
            File.WriteAllText(path, "{\"code\":\"return 1;\",\"ok\":true}\n");
            return path;
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
