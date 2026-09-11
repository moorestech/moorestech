using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BugReportManifestTest
    {
        [Test]
        public void 主要キーがcamelCaseで出力される()
        {
            var manifest = new BugReportManifest
            {
                CreatedAt = "2026-09-11T12:00:00Z",
                Description = "ベルトが止まる",
                Platform = "OSXEditor",
                IsEditor = true,
                ReportTick = 1234,
                SnapshotTicks = new List<ulong> { 600, 1200 },
                SnapshotFiles = new List<string> { "tick_600.json" },
                PacketLogFiles = new List<string> { "packets_601.bin" },
                Repository = new RepositoryState { Commit = "abc", Branch = "feature/x", Dirty = true },
                MasterData = new RepositoryState { Commit = "def", Branch = "HEAD", Dirty = false },
                ClientState = new ClientStateSnapshot(new UnityEngine.Vector3(1, 2, 3), new UnityEngine.Vector3(0, 90, 0), new UnityEngine.Vector3(4, 5, 6), "PauseMenu", 1234),
                Missing = new List<MissingItem> { new MissingItem { Item = "video", Reason = "ffmpeg not found" } },
                VideoSeconds = 0,
            };
            var json = JObject.Parse(manifest.ToJson());
            foreach (var key in new[] { "schemaVersion", "createdAt", "description", "platform", "isEditor", "reportTick", "snapshotTicks", "snapshotFiles", "packetLogFiles", "repository", "masterData", "clientState", "missing", "videoSeconds" })
            {
                Assert.IsTrue(json.ContainsKey(key), $"キー {key} が無い");
            }
            Assert.AreEqual("ffmpeg not found", (string)json["missing"][0]["reason"]);
        }
    }
}
