using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
                ServerData = new ServerDataLocation { Path = "/repo/server_v8", RelativeTo = "masterData", RelativePath = "server_v8" },
                ClientState = new ClientStateSnapshot(new UnityEngine.Vector3(1, 2, 3), new UnityEngine.Vector3(0, 90, 0), new UnityEngine.Vector3(4, 5, 6), "PauseMenu", 1234),
                Missing = new List<MissingItem> { new MissingItem { Item = "video", Reason = "ffmpeg not found" } },
                VideoSeconds = 0,
            };
            var json = JObject.Parse(manifest.ToJson());
            foreach (var key in new[] { "schemaVersion", "createdAt", "description", "platform", "isEditor", "reportTick", "snapshotTicks", "snapshotFiles", "packetLogFiles", "repository", "masterData", "serverData", "clientState", "missing", "videoSeconds" })
            {
                Assert.IsTrue(json.ContainsKey(key), $"キー {key} が無い");
            }
            Assert.AreEqual("ffmpeg not found", (string)json["missing"][0]["reason"]);
            Assert.AreEqual("server_v8", (string)json["serverData"]["relativePath"]);

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

        // 記録時のサーバーデータは受け側の別マシンで解決される。相対の基準を取り違えると別マスタで再生される
        // The recording's server data is resolved on another machine; a wrong root replays against different masters
        [Test]
        public void サーバーデータはリポジトリ相対で記録される()
        {
            var location = ServerDataLocation.Resolve(
                Path.Combine("/repo", "moorestech_client", "Assets", "ServerData"), "/repo", "/master");

            Assert.AreEqual(ServerDataLocation.RepositoryRootName, location.RelativeTo);
            Assert.AreEqual("moorestech_client/Assets/ServerData", location.RelativePath);
        }

        [Test]
        public void マスタデータ配下のサーバーデータはマスタ相対で記録される()
        {
            var location = ServerDataLocation.Resolve(Path.Combine("/master", "server_v8"), "/repo", "/master");

            Assert.AreEqual(ServerDataLocation.MasterDataRootName, location.RelativeTo);
            Assert.AreEqual("server_v8", location.RelativePath);
        }

        // 受け側で解決できない置き場を黙って載せると、再現側は別データで再生して原因不明の例外になる
        // Silently recording an unresolvable location makes the reproduction replay other data and fail inexplicably
        [Test]
        public void リポジトリ外のサーバーデータは欠損として理由が残る()
        {
            var manifest = new BugReportManifest();
            LogAssert.Expect(LogType.Warning, new Regex("リポジトリの外"));
            LogAssert.Expect(LogType.Warning, new Regex("リポジトリの外"));

            ServerDataLocation.Record(Path.Combine("/elsewhere", "server_v8"), manifest, "/repo", "/master");

            Assert.AreEqual(ServerDataLocation.AbsoluteRootName, manifest.ServerData.RelativeTo);
            Assert.AreEqual("serverData/relativePath", manifest.Missing[0].Item);
        }

        // サーバーが置き場を申告しなかった箱を無言で通すと、受け側は何を渡せばよいか分からないまま失敗する
        // Passing a box with no declared location in silence leaves the receiver guessing what to hand the replay
        [Test]
        public void サーバーデータの申告が無い箱は欠損として理由が残る()
        {
            var manifest = new BugReportManifest();
            LogAssert.Expect(LogType.Warning, new Regex("serverData"));

            ServerDataLocation.Record("", manifest, "/repo", "/master");

            Assert.IsNull(manifest.ServerData);
            Assert.AreEqual("serverData", manifest.Missing[0].Item);
        }
    }
}
