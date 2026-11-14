using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.GameUiExceptions;
using Client.Game.InGame.BugReport.Readings;
using Client.Game.InGame.BugReport.WebUiHealth;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport.Bundle
{
    // manifest.gameUiExceptions の綴りと数え方を固定する。ダッシュボードはこの語で UI の故障を出す（A-244）
    // Pins manifest.gameUiExceptions' spelling and counting; the dashboard shows UI failures by these words (A-244)
    public class GameUiExceptionManifestTest
    {
        private const string UiStack = "Client.Ui.Screens.Probe.OnClick () (at Assets/Probe.cs:1)\nUnityEngine.UIElements.EventDispatcher.ProcessEvent ()";

        [Test]
        public void Client_Uiを含む例外だけを数え見本は5件まで()
        {
            var tally = new GameUiExceptionTally();
            for (var i = 0; i < 7; i++) tally.Add(LogType.Exception, $"boom {i}", UiStack);
            tally.Add(LogType.Exception, "world", "Client.Game.InGame.World.Tick ()");
            tally.Add(LogType.Error, "ui error", "Client.Ui.Screens.Probe.OnClick ()");
            var recorded = tally.Snapshot();
            Assert.AreEqual(GameUiExceptionStatus.Recorded, recorded.Status);
            Assert.AreEqual(7, recorded.Count);
            Assert.AreEqual(GameUiExceptionTally.SampleLimit, recorded.Samples.Count);
            Assert.AreEqual("boom 0", recorded.Samples[0].Message);
            Assert.AreEqual("Client.Ui.Screens.Probe.OnClick () (at Assets/Probe.cs:1)", recorded.Samples[0].Frame);
        }

        // 環（2000 件で古い順に捨てる）が溢れても、序盤の UI の例外は数と見本に残る（0 件と読ませない）
        // Even after the ring (dropping the oldest past 2000) overflows, an early UI exception stays in the count and samples (never reads as zero)
        [Test]
        public void 環が溢れた後でも序盤の例外が数に残る()
        {
            var ring = new UnityLogRing();
            var tally = new GameUiExceptionTally();
            Log(LogType.Exception, "early ui boom", UiStack);
            for (var i = 0; i < UnityLogRing.Capacity + 1; i++) Log(LogType.Log, $"noise {i}", "");
            Assert.IsFalse(ring.Dump().Any(entry => entry.Message == "early ui boom"), "前提: 環から序盤の例外が押し出されている");
            var recorded = tally.Snapshot();
            Assert.AreEqual(1, recorded.Count);
            Assert.AreEqual("early ui boom", recorded.Samples[0].Message);

            #region Internal

            // 本番と同じく、受けたログを環と数え手の両方へ渡す
            // As in production, every received log goes to both the ring and the tally
            void Log(LogType type, string message, string stackTrace)
            {
                ring.Add(type, message, stackTrace);
                tally.Add(type, message, stackTrace);
            }

            #endregion
        }

        [Test]
        public void UIのフレームが無いスタックは見つからないと返す()
        {
            Assert.IsFalse(GameUiExceptionTally.TryFindFirstUiFrame("Client.Game.InGame.World.Tick ()\nUnityEngine.PlayerLoop ()", out _));
            Assert.IsFalse(GameUiExceptionTally.TryFindFirstUiFrame("Client.Game.InGame.World:Apply (Client.Ui.State.Probe probe)", out _), "引数の型だけに UI が載る行は UI のフレームでない");
            Assert.IsTrue(GameUiExceptionTally.TryFindFirstUiFrame("UnityEngine.Foo ()\n  Client.Ui.State.Probe.Run () ", out var frame));
            Assert.AreEqual("Client.Ui.State.Probe.Run ()", frame);
        }

        [Test]
        public void 例外が無ければrecordedで0件()
        {
            var manifest = new BugReportManifest();
            BugReportManifestReadings.Apply(manifest, new BugReportCapturedData { GameUiExceptions = new GameUiExceptionTally().Snapshot(), WebUiHealth = new WebUiHealthReading() });
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual(7, (int)json["schemaVersion"]);
            Assert.AreEqual("recorded", (string)json["gameUiExceptions"]["status"]);
            Assert.AreEqual(0, (int)json["gameUiExceptions"]["count"]);
            Assert.AreEqual(0, ((JArray)json["gameUiExceptions"]["samples"]).Count);
            Assert.IsFalse(manifest.Missing.Any(item => item.Item == "gameUiExceptions"));
        }

        [Test]
        public void 確保時に数を取れなければunknownで件数の欄を持たず欠損に書く()
        {
            var manifest = new BugReportManifest();
            BugReportManifestReadings.Apply(manifest, new BugReportCapturedData { GameUiExceptions = new GameUiExceptionsUnknown(), WebUiHealth = new WebUiHealthReading() });
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual("unknown", (string)json["gameUiExceptions"]["status"]);
            Assert.IsNull(json["gameUiExceptions"]["count"], "unknown は件数の欄を持たない（0 件と読ませない）");
            Assert.IsNull(json["gameUiExceptions"]["samples"]);
            Assert.IsTrue(manifest.Missing.Any(item => item.Item == "gameUiExceptions"));
        }

        [Test]
        public void 新しいmanifestの既定はunknown()
        {
            var json = JObject.Parse(new BugReportManifest().ToJson());
            Assert.AreEqual("unknown", (string)json["gameUiExceptions"]["status"]);
        }
    }
}
