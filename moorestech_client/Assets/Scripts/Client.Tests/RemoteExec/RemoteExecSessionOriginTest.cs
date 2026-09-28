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
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), enabled ? new RemoteExecOriginMark(RemoteExecLedger.CurrentFileName) : null);
            var capture = SessionSnapshotCapture.Started(Path.Combine(_directory, "snapshots"), 1234, "session_100");
            origin = origin.WithSnapshotCapture(capture).WithSalvageMissing(new List<MissingItem>());
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var restored = SessionOriginSnapshot.ReadFrom(_path, out var reason);
            Assert.IsNull(reason);
            Assert.AreEqual(enabled, restored.RemoteExec != null);
            Assert.AreEqual(enabled ? RemoteExecLedger.CurrentFileName : null, restored.RemoteExec?.LedgerFileName);
            Assert.AreEqual(capture.Owner, restored.SnapshotCapture.Owner);
        }

        [Test]
        public void キー欠損は無効として読む()
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), new RemoteExecOriginMark(RemoteExecLedger.CurrentFileName));
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var json = JObject.Parse(File.ReadAllText(_path));
            json.Remove("remoteExecLedgerFileName");
            File.WriteAllText(_path, json.ToString());
            var restored = SessionOriginSnapshot.ReadFrom(_path, out var reason);
            Assert.IsNull(reason);
            Assert.IsNull(restored.RemoteExec);
        }

        [Test]
        public void 実行試行の印は出所の退避後も残る()
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), new RemoteExecOriginMark(RemoteExecLedger.CurrentFileName));
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            File.WriteAllText(Path.Combine(_directory, RemoteExecLedger.AttemptSignalFileName), string.Empty);
            File.WriteAllText(Path.Combine(_directory, RemoteExecLedger.FailureSignalFileName), string.Empty);

            var restored = SessionOriginSnapshot.ReadFrom(_path, out var reason);
            Assert.IsNull(reason);
            Assert.IsTrue(restored.RemoteExec.Attempted);
            Assert.IsTrue(restored.RemoteExec.LedgerWriteFailed);

            // 退避先へ出所だけ移しても、印ファイルを伴わない先では真偽を名乗らない（JSONへ複写しないため）
            // Moving only the origin to a salvage directory never claims either truth there, because neither is copied into JSON
            var salvaged = restored.WithSalvageMissing(new List<MissingItem>());
            var salvagedPath = Path.Combine(_directory, "salvaged", "previous-origin.json");
            Assert.IsTrue(salvaged.WriteTo(salvagedPath).Succeeded);
            File.WriteAllText(Path.Combine(_directory, "salvaged", RemoteExecLedger.AttemptSignalFileName), string.Empty);
            File.WriteAllText(Path.Combine(_directory, "salvaged", RemoteExecLedger.FailureSignalFileName), string.Empty);
            var reread = SessionOriginSnapshot.ReadFrom(salvagedPath, out reason);
            Assert.IsNull(reason);
            Assert.IsTrue(reread.RemoteExec.Attempted);
            Assert.IsTrue(reread.RemoteExec.LedgerWriteFailed);
        }

        [TestCase("1")]
        [TestCase("\"../ledger-1.jsonl\"")]
        [TestCase("\"ledger-invalid.jsonl\"")]
        public void 不正な台帳名は理由付き読み込み失敗になる(string value)
        {
            var origin = new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), new RemoteExecOriginMark(RemoteExecLedger.CurrentFileName));
            Assert.IsTrue(origin.WriteTo(_path).Succeeded);
            var json = JObject.Parse(File.ReadAllText(_path));
            json["remoteExecLedgerFileName"] = JToken.Parse(value);
            File.WriteAllText(_path, json.ToString());
            Assert.IsNull(SessionOriginSnapshot.ReadFrom(_path, out var reason));
            StringAssert.Contains("remoteExecLedgerFileName", reason);
        }
    }
}
