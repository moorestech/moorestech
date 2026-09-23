using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport
{
    public class SalvageMissingPersistenceTest
    {
        private string _root;
        private string _originPath;
        private SessionOriginSnapshot _origin;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"salvage-missing-{Guid.NewGuid():N}");
            _originPath = Path.Combine(_root, PreviousSessionSalvage.PreviousOriginFileName);
            _origin = new SessionOriginSnapshot("previous", null, BuildOriginReading.Editor(), SessionSnapshotCapture.Started(Path.Combine(_root, "source"), 1234, "session_100"));
            _origin.WriteTo(_originPath);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        [TestCase("absent")]
        [TestCase("version")]
        [TestCase("oversizedVersion")]
        [TestCase("owner")]
        [TestCase("items")]
        [TestCase("entry")]
        public void 不明な欠損履歴を欠損なしへ補完しない(string malformed)
        {
            var json = JObject.Parse(File.ReadAllText(_originPath));
            if (malformed == "absent") json.Remove("salvageMissing");
            else if (malformed == "version") json["salvageMissing"]["version"] = 999;
            else if (malformed == "oversizedVersion") json["salvageMissing"]["version"] = long.MaxValue;
            else if (malformed == "owner") json["salvageMissing"]["owner"] = "another/session";
            else if (malformed == "items") json["salvageMissing"]["items"] = "invalid";
            else json["salvageMissing"]["items"] = new JArray(new JObject { ["item"] = "snapshots" });
            File.WriteAllText(_originPath, json.ToString());
            PendingCrashReportMark.MarkPending(_root);

            var artifacts = PreviousSessionSalvage.Salvage(new PreviousSessionSalvageRequest { LastSessionDirectory = _root });

            Assert.IsNotNull(artifacts.PreviousOrigin);
            Assert.IsTrue(artifacts.Missing.Exists(item => item.Item == "previousOrigin" && item.Reason.Contains("退避欠損の履歴が不明")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 原子的書込み失敗で有効な出所を破壊しない(bool replacementFails)
        {
            var before = File.ReadAllText(_originPath);
            var target = _originPath;
            if (replacementFails)
            {
                target = Path.Combine(_root, "directory-target");
                Directory.CreateDirectory(target);
            }
            else Directory.CreateDirectory(target + ".tmp");
            var updated = _origin.WithSalvageMissing(new List<MissingItem> { new MissingItem { Item = "snapshots", Reason = "partial" } });
            LogAssert.Expect(LogType.Error, new Regex("セッションの出所を書けませんでした"));

            var write = updated.WriteTo(target);

            Assert.IsFalse(write.Succeeded);
            Assert.IsNotEmpty(write.FailureReason);
            Assert.AreEqual(before, File.ReadAllText(_originPath));
        }

        [Test]
        public void 欠損履歴の保存失敗も理由付きで表明する()
        {
            var source = _origin.SnapshotCapture.Directory;
            Directory.CreateDirectory(source);
            _origin.WriteTo(Path.Combine(source, WorldDataDirectory.SnapshotOwnerFileName));
            File.WriteAllText(Path.Combine(source, "tick_100.json"), "owned");
            var lastSession = Path.Combine(_root, "last-session");
            Directory.CreateDirectory(Path.Combine(lastSession, PreviousSessionSalvage.PreviousOriginFileName + ".tmp"));
            var request = new PreviousSessionSalvageRequest
            {
                LastSessionDirectory = lastSession,
                PreviousSessions = new List<PreviousProcessSession> { new PreviousProcessSession { ProcessId = 1234, SessionName = "session_100", Origin = _origin } },
            };
            LogAssert.Expect(LogType.Error, new Regex("セッションの出所を書けませんでした"));

            var artifacts = PreviousSessionSalvage.Salvage(request);

            Assert.IsNotNull(artifacts.SnapshotsDirectory);
            Assert.IsTrue(artifacts.Missing.Exists(item => item.Item == "previousOrigin" && item.Reason.Contains("書けませんでした")));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_100.json")));
            request.PreviousSessions.Clear();
            var replay = PreviousSessionSalvage.Salvage(request);
            Assert.IsNull(replay.SnapshotsDirectory);
            Assert.IsTrue(replay.Missing.Exists(item => item.Item == "previousOrigin" && item.Reason.Contains("出所が不明")));
        }
    }
}
