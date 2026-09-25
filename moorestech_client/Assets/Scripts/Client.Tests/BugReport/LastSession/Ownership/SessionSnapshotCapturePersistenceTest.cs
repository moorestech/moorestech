using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
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
            new SessionOriginSnapshot("steam", null, BuildOriginReading.Editor(), capture).WriteTo(path);
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
            Assert.IsNull(restored.SteamIdAbsenceReason, "SteamIDが有るのに欠損理由が付いている");
        }

        // 前回セッションが書いたSteamID欠損理由は、印を往復しても所有印の付け直しでも失われない（F01）
        // The SteamID absence reason written by the previous session survives the round trip and the ownership re-stamp (F01)
        [Test]
        public void SteamIdAbsenceReason_SurvivesRoundTripAndOwnershipRestamp()
        {
            const int processId = 2147482986;
            const string sessionName = "session_101";
            const string reason = "テスター識別（SteamID）が無い（SteamUser.GetSteamID で読めなかった）";
            CleanExitMarker.MarkSessionStarted(processId, sessionName, new SessionOriginSnapshot(null, reason, BuildOriginReading.Editor()));
            CleanExitMarker.RecordSnapshotCapture(processId, sessionName, _root);
            var source = SessionOriginSnapshot.ReadFrom(Path.Combine(_root, WorldDataDirectory.SnapshotOwnerFileName), out var failure);
            var session = CleanExitMarker.ConsumeSessionMarks(processId, sessionName);
            Assert.IsNull(failure);
            Assert.IsNull(session.Origin.SteamId);
            Assert.AreEqual(reason, session.Origin.SteamIdAbsenceReason, "セッション印で欠損理由が失われた");
            Assert.AreEqual(reason, source.SteamIdAbsenceReason, "退避元の所有印で欠損理由が失われた");
            Assert.AreEqual(reason, session.Origin.WithSalvageMissing(new List<MissingItem>()).SteamIdAbsenceReason);
        }

        // 理由キーの無い旧形式の印は、nullでなく旧形式であることを明示した理由として読む
        // A legacy mark without the reason key reads as an explicit legacy reason instead of null
        [Test]
        public void LegacyMarkWithoutSteamIdAbsenceReason_ReadsExplicitLegacyReason()
        {
            var path = Path.Combine(_root, "origin.json");
            File.WriteAllText(path, new JObject { ["steamId"] = null, ["buildOriginKind"] = "Editor" }.ToString());
            var restored = SessionOriginSnapshot.ReadFrom(path, out var failure);
            Assert.IsNull(failure);
            Assert.IsNull(restored.SteamId);
            Assert.AreEqual(SessionOriginSnapshot.LegacyMarkSteamIdAbsenceReason, restored.SteamIdAbsenceReason);
        }

        [Test]
        public void RecordSnapshotCapture_UpdatesSessionAndSourceWithSameOwner()
        {
            const int processId = 2147482987;
            const string sessionName = "session_100";
            CleanExitMarker.MarkSessionStarted(processId, sessionName, new SessionOriginSnapshot("current", null, BuildOriginReading.Editor()));
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
