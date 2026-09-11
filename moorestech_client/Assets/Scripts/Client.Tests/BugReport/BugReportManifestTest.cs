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

            // Vector3が循環参照設定で黙って消えていないか実値で検証する
            // Verify the Vector3 actually survives serialization, not just the containing key
            Assert.AreEqual(1.0, (double)json["clientState"]["cameraPosition"]["x"]);
            Assert.AreEqual(2.0, (double)json["clientState"]["cameraPosition"]["y"]);
            Assert.AreEqual(3.0, (double)json["clientState"]["cameraPosition"]["z"]);
            Assert.AreEqual(0.0, (double)json["clientState"]["cameraEulerAngles"]["x"]);
            Assert.AreEqual(90.0, (double)json["clientState"]["cameraEulerAngles"]["y"]);
            Assert.AreEqual(0.0, (double)json["clientState"]["cameraEulerAngles"]["z"]);
            Assert.AreEqual(4.0, (double)json["clientState"]["playerPosition"]["x"]);
            Assert.AreEqual(5.0, (double)json["clientState"]["playerPosition"]["y"]);
            Assert.AreEqual(6.0, (double)json["clientState"]["playerPosition"]["z"]);
        }
    }
}
