using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport
{
    public class SnapshotPartialSalvageTest
    {
        private string _root;
        private string _snapshots;
        private string _locked;
        private PreviousSessionSalvageRequest _request;
        private SessionOriginSnapshot _origin;
        private string _bundle;

        [SetUp]
        public void SetUp()
        {
            _locked = null;
            _bundle = null;
            _root = Path.Combine(Path.GetTempPath(), $"partial-snapshot-{Guid.NewGuid():N}");
            _snapshots = Path.Combine(_root, "snapshots");
            Directory.CreateDirectory(_snapshots);
            _origin = new SessionOriginSnapshot(null, "テストで差し込まれていないSteamID", BuildOriginReading.Editor(), SessionSnapshotCapture.Started(_snapshots, 1234, "session_100"));
            _origin.WriteTo(Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName));
            _request = new PreviousSessionSalvageRequest
            {
                LastSessionDirectory = Path.Combine(_root, "last-session"),
                PreviousSessions = new List<PreviousProcessSession> { new PreviousProcessSession { ProcessId = 1234, SessionName = "session_100", Origin = _origin } },
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_locked != null) Chmod(_locked, "755");
            if (_bundle != null) Directory.Delete(_bundle, true);
            Directory.Delete(_root, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 移動後半が拒否されても所有確認済みの部分資料は返る(bool replay)
        {
            PreparePartialSnapshot();
            var artifacts = PreviousSessionSalvage.Salvage(_request);

            Assert.IsNotNull(artifacts.SnapshotsDirectory);
            Assert.AreEqual("owned-snapshot", File.ReadAllText(Path.Combine(artifacts.SnapshotsDirectory, "tick_100.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_locked, "packets_101.bin")));
            var moveFailure = artifacts.Missing.Single(item => item.Item == "snapshots" && item.Reason.Contains("移動")).Reason;
            if (replay)
            {
                _request.PreviousSessions.Clear();
                artifacts = PreviousSessionSalvage.Salvage(_request);
                Assert.IsTrue(artifacts.Missing.Exists(item => item.Item == "snapshots" && item.Reason == moveFailure));
            }
            _bundle = CrashBundleWriter.Write(artifacts, "partial", RepositoryStateProbe.RepositoryRoot, RepositoryStateProbe.MasterDataRoot);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(_bundle, BugReportBundleLayout.ManifestFileName)));
            Assert.AreEqual(1, ((JArray)manifest["snapshotFiles"]).Count);
            Assert.IsTrue(((JArray)manifest["missing"]).Any(item => (string)item["item"] == "snapshots" && (string)item["reason"] == moveFailure));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 前世代の欠損は新世代や消費後へ混ざらない(bool consume)
        {
            PreparePartialSnapshot();
            var first = PreviousSessionSalvage.Salvage(_request);
            var moveFailure = first.Missing.Single(item => item.Item == "snapshots" && item.Reason.Contains("移動")).Reason;
            _request.PreviousSessions.Clear();
            if (consume) PendingCrashReportMark.Clear(_request.LastSessionDirectory);
            else _request.PreviousSessions.Add(new PreviousProcessSession { ProcessId = 5678, SessionName = "session_200", Origin = new SessionOriginSnapshot(null, "テストで差し込まれていないSteamID", BuildOriginReading.Editor()) });

            var next = PreviousSessionSalvage.Salvage(_request);
            Assert.IsFalse(next.Missing.Exists(item => item.Reason == moveFailure));
            if (consume)
            {
                Assert.IsTrue(next.PreviousExitWasClean);
                Assert.IsFalse(File.Exists(Path.Combine(_request.LastSessionDirectory, PreviousSessionSalvage.PreviousOriginFileName)));
                return;
            }
            Assert.IsNull(next.SnapshotsDirectory);
            _request.PreviousSessions.Clear();
            var replay = PreviousSessionSalvage.Salvage(_request);
            Assert.IsFalse(replay.Missing.Exists(item => item.Reason == moveFailure));
            Assert.IsNull(replay.SnapshotsDirectory);
        }

        private void PreparePartialSnapshot()
        {
            if (Environment.UserName == "root" || Application.platform == RuntimePlatform.WindowsEditor)
                Assert.Ignore("Unix非rootの削除権限拒否を使うテスト");
            File.WriteAllText(Path.Combine(_snapshots, "tick_100.json"), "owned-snapshot");
            _locked = Path.Combine(_snapshots, "locked");
            Directory.CreateDirectory(_locked);
            File.WriteAllText(Path.Combine(_locked, "packets_101.bin"), "locked-packet");
            Chmod(_locked, "555");
        }

        [Test]
        public void 移動済みの有効な所有印は上書きしない()
        {
            var owner = Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName);
            var modified = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(owner, modified);
            File.WriteAllText(Path.Combine(_snapshots, "tick_100.json"), "owned-snapshot");

            var artifacts = PreviousSessionSalvage.Salvage(_request);

            Assert.AreEqual(modified, File.GetLastWriteTimeUtc(Path.Combine(artifacts.SnapshotsDirectory, WorldDataDirectory.SnapshotOwnerFileName)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 所有印だけの新規回収も未応答再提示も実資料と数えない(bool pending)
        {
            if (pending)
            {
                Directory.CreateDirectory(_request.LastSessionDirectory);
                Directory.Move(_snapshots, Path.Combine(_request.LastSessionDirectory, "snapshots"));
                _origin.WriteTo(Path.Combine(_request.LastSessionDirectory, PreviousSessionSalvage.PreviousOriginFileName));
                PendingCrashReportMark.MarkPending(_request.LastSessionDirectory);
                _request.PreviousSessions.Clear();
            }
            var artifacts = PreviousSessionSalvage.Salvage(_request);
            Assert.IsNull(artifacts.SnapshotsDirectory);
            Assert.IsTrue(artifacts.Missing.Exists(item => item.Item == "snapshots"));
        }

        private static void Chmod(string path, string mode)
        {
            using var process = Process.Start(new ProcessStartInfo("chmod", $"{mode} \"{path}\"") { UseShellExecute = false });
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode);
        }

        [Test]
        public void 所有印だけを直接箱へ渡してもmanifestは資料欠損を示す()
        {
            var artifacts = TestPreviousSessionArtifacts.Unclean(_request.LastSessionDirectory, null, _snapshots, null, new List<string>());
            _bundle = CrashBundleWriter.Write(artifacts, "owner only", RepositoryStateProbe.RepositoryRoot, RepositoryStateProbe.MasterDataRoot);
            Assert.IsNotNull(_bundle);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(_bundle, BugReportBundleLayout.ManifestFileName)));
            Assert.AreEqual(0, ((JArray)manifest["snapshotFiles"]).Count);
            Assert.AreEqual(0, ((JArray)manifest["packetLogFiles"]).Count);
            Assert.IsTrue(((JArray)manifest["missing"]).Any(item => (string)item["item"] == "snapshots"));
        }
    }
}
