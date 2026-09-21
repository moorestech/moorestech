using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class SnapshotOwnershipSalvageTest
    {
        private string _root;
        private string _snapshots;
        private string _lastSession;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"snapshot-ownership-{Guid.NewGuid():N}");
            _snapshots = Path.Combine(_root, "local-world", "snapshots");
            _lastSession = Path.Combine(_root, "last-session");
            Directory.CreateDirectory(_snapshots);
            File.WriteAllText(Path.Combine(_snapshots, "tick_100.json"), "local-A");
            File.WriteAllText(Path.Combine(_snapshots, "packets_101.bin"), "local-A-packets");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        [TestCase("remote")]
        [TestCase("webui-not-ready")]
        [TestCase("webui-failed")]
        public void LocalCleanThenUnrecordedCrash_DoesNotSalvageLocalFiles(string unrecordedBoot)
        {
            // local Aの正常終了を次の起動が消費しても、ワールド内の元資料は残る
            // Consuming local A's clean exit leaves its original world evidence on disk
            var localA = new PreviousProcessSession { ProcessId = 1234, SessionName = "session_100", ExitedCleanly = true };
            PreviousSessionSalvage.Salvage(Request(localA));

            var crashed = new PreviousProcessSession
            {
                ProcessId = 1234,
                SessionName = "session_200",
                Origin = new SessionOriginSnapshot(unrecordedBoot, BuildOriginReading.Editor()),
            };
            var artifacts = PreviousSessionSalvage.Salvage(Request(crashed));

            // 今回がlocal Aでも、前回のremote/未開始sessionへAの資料を結び付けない
            // Restarting local A must not attach A's evidence to the previous remote or unstarted session
            Assert.IsNull(artifacts.SnapshotsDirectory);
            Assert.AreEqual("local-A", File.ReadAllText(Path.Combine(_snapshots, "tick_100.json")));
            Assert.AreEqual("local-A-packets", File.ReadAllText(Path.Combine(_snapshots, "packets_101.bin")));
            Assert.IsTrue(artifacts.Missing.Exists(item => item.Item == "snapshots"));
        }

        private PreviousSessionSalvageRequest Request(PreviousProcessSession session)
        {
            return new PreviousSessionSalvageRequest
            {
                LastSessionDirectory = _lastSession,
                PreviousSessions = new List<PreviousProcessSession> { session },
            };
        }

        [Test]
        public void OwnedSnapshotAndPacket_AreSalvagedAndPresentedAgain()
        {
            var crashed = OwnedSession("session_100");
            crashed.Origin.WriteTo(Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName));
            var artifacts = PreviousSessionSalvage.Salvage(Request(crashed));
            Assert.AreEqual("local-A", File.ReadAllText(Path.Combine(artifacts.SnapshotsDirectory, "tick_100.json")));
            Assert.AreEqual("local-A-packets", File.ReadAllText(Path.Combine(artifacts.SnapshotsDirectory, "packets_101.bin")));

            var next = new PreviousProcessSession { ProcessId = 1234, SessionName = "session_200", ExitedCleanly = true };
            var carried = PreviousSessionSalvage.Salvage(Request(next));
            Assert.AreEqual(artifacts.SnapshotsDirectory, carried.SnapshotsDirectory);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrReusedSourceOwnership_DoesNotMoveFiles(bool reused)
        {
            if (reused) OwnedSession("session_200").Origin.WriteTo(Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName));
            var artifacts = PreviousSessionSalvage.Salvage(Request(OwnedSession("session_100")));
            Assert.IsNull(artifacts.SnapshotsDirectory);
            Assert.IsTrue(File.Exists(Path.Combine(_snapshots, "packets_101.bin")));
            Assert.IsTrue(artifacts.Missing.Exists(item => item.Item == "snapshots" && item.Reason.Contains("所有印")));
        }

        [Test]
        public void NewUnrecordedCrash_DoesNotInheritOldPendingSnapshotsOnReplay()
        {
            var old = OwnedSession("session_100");
            old.Origin.WriteTo(Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName));
            PreviousSessionSalvage.Salvage(Request(old));
            var unrecorded = new PreviousProcessSession { ProcessId = 1234, SessionName = "session_200", Origin = new SessionOriginSnapshot(null, BuildOriginReading.Editor()) };
            Assert.IsNull(PreviousSessionSalvage.Salvage(Request(unrecorded)).SnapshotsDirectory);
            var clean = new PreviousProcessSession { ProcessId = 1234, SessionName = "session_300", ExitedCleanly = true };
            Assert.IsNull(PreviousSessionSalvage.Salvage(Request(clean)).SnapshotsDirectory);
        }

        [Test]
        public void SnapshotRestart_InvalidatesPreviousOwnerBeforeReusingDirectory()
        {
            OwnedSession("session_100").Origin.WriteTo(Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName));
            SnapshotDirectoryCleaner.DeletePreviousSessionFiles(_snapshots);
            Assert.IsFalse(File.Exists(Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName)));
            Assert.IsFalse(File.Exists(Path.Combine(_snapshots, "tick_100.json")));
            Assert.IsFalse(File.Exists(Path.Combine(_snapshots, "packets_101.bin")));
        }

        private PreviousProcessSession OwnedSession(string sessionName)
        {
            return new PreviousProcessSession
            {
                ProcessId = 1234,
                SessionName = sessionName,
                Origin = new SessionOriginSnapshot("local", BuildOriginReading.Editor(), SessionSnapshotCapture.Started(_snapshots, 1234, sessionName)),
            };
        }
    }
}
