using System;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class SessionSnapshotCapturePersistenceTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"capture-origin-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CaptureState_RoundTripsWithoutChangingBuildOrigin(bool started)
        {
            var capture = started ? SessionSnapshotCapture.Started(_root, 1234, "session_100") : SessionSnapshotCapture.NotStarted();
            var path = Path.Combine(_root, "origin.json");
            new SessionOriginSnapshot("steam", BuildOriginReading.Editor(), capture).WriteTo(path);
            var restored = SessionOriginSnapshot.ReadFrom(path, out var failure);
            Assert.IsNull(failure);
            Assert.AreEqual("steam", restored.SteamId);
            Assert.AreEqual(BuildOriginKind.Editor, restored.BuildOrigin.Kind);
            Assert.AreEqual(capture.Directory, restored.SnapshotCapture.Directory);
            Assert.AreEqual(capture.Owner, restored.SnapshotCapture.Owner);
            Assert.AreEqual(capture.MissingReason, restored.SnapshotCapture.MissingReason);
        }

        [TestCase(null)]
        [TestCase("{\"version\":2}")]
        [TestCase("{\"version\":1,\"state\":\"started\",\"directory\":\"relative\",\"owner\":\"x\"}")]
        [TestCase("{\"version\":1,\"state\":\"started\",\"directory\":42,\"owner\":\"x\"}")]
        public void LegacyOrMalformedCapture_PreservesOriginButDeclaresMissing(string captureJson)
        {
            var path = Path.Combine(_root, "origin.json");
            var json = new JObject { ["steamId"] = "old", ["buildOriginKind"] = "Editor" };
            if (captureJson != null) json["snapshotCapture"] = JObject.Parse(captureJson);
            File.WriteAllText(path, json.ToString());
            var restored = SessionOriginSnapshot.ReadFrom(path, out var failure);
            Assert.IsNull(failure);
            Assert.AreEqual("old", restored.SteamId);
            Assert.IsNotEmpty(restored.SnapshotCapture.MissingReason);
            Assert.IsNull(restored.SnapshotCapture.Directory);
        }

        [Test]
        public void RecordSnapshotCapture_UpdatesSessionAndSourceWithSameOwner()
        {
            const int processId = 2147482987;
            const string sessionName = "session_100";
            CleanExitMarker.MarkSessionStarted(processId, sessionName, new SessionOriginSnapshot("current", BuildOriginReading.Editor()));
            CleanExitMarker.RecordSnapshotCapture(processId, sessionName, _root);
            var source = SessionOriginSnapshot.ReadFrom(Path.Combine(_root, WorldDataDirectory.SnapshotOwnerFileName), out var failure);
            var session = CleanExitMarker.ConsumeSessionMarks(processId, sessionName);
            Assert.IsNull(failure);
            Assert.AreEqual($"pid_{processId}/{sessionName}", source.SnapshotCapture.Owner);
            Assert.AreEqual(source.SnapshotCapture.Owner, session.Origin.SnapshotCapture.Owner);
            Assert.AreEqual(_root, session.Origin.SnapshotCapture.Directory);
        }
    }
}
