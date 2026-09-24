using System;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.BugReport
{
    public class LocalSnapshotCaptureRegistrationTest
    {
        private string _root;
        private string _session;
        private WorldSnapshotRing _ring;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"capture-registration-{Guid.NewGuid():N}");
            ProcessSessionScope.BeginNewSession();
            _session = ProcessSessionScope.CurrentSessionName;
            CleanExitMarker.MarkSessionStarted(RecordingProcessDirectories.CurrentProcessId(), _session, new SessionOriginSnapshot(null, "テストで差し込まれていないSteamID", BuildOriginReading.Editor()));
        }

        [TearDown]
        public void TearDown()
        {
            _ring.Stop();
            CleanExitMarker.ConsumeSessionMarks(RecordingProcessDirectories.CurrentProcessId(), _session);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OnlyRunningCapture_OwnsTheActualServerDirectory(bool started)
        {
            var directory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, Path.Combine(_root, "save.json"));
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) { worldDataDirectory = directory };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            _ring = provider.GetRequiredService<WorldSnapshotRing>();
            if (started) _ring.Start(600u, 1800u, 16);

            LocalSnapshotCaptureRegistration.RecordStartedLocalServer();

            var ownerPath = Path.Combine(directory.SnapshotDirectory, WorldDataDirectory.SnapshotOwnerFileName);
            Assert.AreEqual(started, File.Exists(ownerPath));
            if (!started) return;
            var origin = SessionOriginSnapshot.ReadFrom(ownerPath, out var failure);
            Assert.IsNull(failure);
            Assert.AreEqual(directory.SnapshotDirectory, origin.SnapshotCapture.Directory);
            StringAssert.EndsWith(_session, origin.SnapshotCapture.Owner);
        }
    }
}
