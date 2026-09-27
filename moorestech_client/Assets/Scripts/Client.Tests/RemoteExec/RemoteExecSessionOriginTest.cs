using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.RemoteExec.Access;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.RemoteExec
{
    public sealed class RemoteExecSessionOriginTest
    {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "remote-exec-origin-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "origin.json");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 有効設定が書き読みと所有印更新で保持される(bool enabled)
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), enabled);
            var capture = SessionSnapshotCapture.Started(Path.Combine(_directory, "snapshots"), 1234, "session_100");
            origin = origin.WithSnapshotCapture(capture).WithSalvageMissing(new List<MissingItem>());
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var restored = SessionOriginSnapshot.ReadFrom(_path, out var reason);
            Assert.IsNull(reason);
            Assert.AreEqual(enabled, restored.RemoteExecEnabled);
            Assert.AreEqual(enabled ? RemoteExecLedger.CurrentFileName : null, restored.RemoteExecLedgerFileName);
            Assert.AreEqual(capture.Owner, restored.SnapshotCapture.Owner);
        }

        [TestCase("\"invalid\"")]
        [TestCase("\"true\"")]
        [TestCase("null")]
        [TestCase("1")]
        [TestCase("[]")]
        [TestCase("{}")]
        public void 不正型の有効設定は例外でなく理由付き失敗になる(string invalidJson)
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), true);
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var json = JObject.Parse(File.ReadAllText(_path));
            json["remoteExecEnabled"] = JToken.Parse(invalidJson);
            File.WriteAllText(_path, json.ToString());
            var restored = SessionOriginSnapshot.ReadFrom(_path, out var reason);
            Assert.IsNull(restored);
            StringAssert.Contains("remoteExecEnabled", reason);
            StringAssert.Contains(_path, reason);
        }

        [Test]
        public void 旧形式のキー欠損は無効として読む()
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), true);
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var json = JObject.Parse(File.ReadAllText(_path));
            json.Remove("remoteExecEnabled");
            json.Remove("remoteExecLedgerFileName");
            File.WriteAllText(_path, json.ToString());
            var restored = SessionOriginSnapshot.ReadFrom(_path, out var reason);
            Assert.IsNull(reason);
            Assert.IsFalse(restored.RemoteExecEnabled);
            Assert.IsNull(restored.RemoteExecLedgerFileName);
        }

        [Test]
        public void 旧形式で台帳名だけ欠けても有効印を読める()
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), true);
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var json = JObject.Parse(File.ReadAllText(_path));
            json.Remove("remoteExecLedgerFileName");
            File.WriteAllText(_path, json.ToString());
            var restored = SessionOriginSnapshot.ReadFrom(_path, out var reason);
            Assert.IsNull(reason);
            Assert.IsTrue(restored.RemoteExecEnabled);
            Assert.IsNull(restored.RemoteExecLedgerFileName);
        }

        [TestCase("1")]
        [TestCase("\"../ledger-1.jsonl\"")]
        [TestCase("\"ledger-invalid.jsonl\"")]
        public void 不正な台帳名は理由付き読み込み失敗になる(string value)
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), true);
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var json = JObject.Parse(File.ReadAllText(_path));
            json["remoteExecLedgerFileName"] = JToken.Parse(value);
            File.WriteAllText(_path, json.ToString());
            Assert.IsNull(SessionOriginSnapshot.ReadFrom(_path, out var reason));
            StringAssert.Contains("remoteExecLedgerFileName", reason);
        }
    }
}
