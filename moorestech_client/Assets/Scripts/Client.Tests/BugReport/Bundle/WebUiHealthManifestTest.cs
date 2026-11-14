using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.BugReport.WebUiHealth;
using Client.Tests.BugReport.Capture;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport.Bundle
{
    // manifest.webUiHealth の綴りと常時非nullを固定する。取り込み側はこの語で CEF 停止の生の値を読む（plan §3.11・ADR 0057）
    // Pins manifest.webUiHealth's spelling and that it is always present; the ingest side reads the raw CEF-stall values by these words (plan §3.11, ADR 0057)
    public class WebUiHealthManifestTest
    {
        [Test]
        public void WebUiHealthの生の値がcamelCaseの綴りでmanifestへ出る()
        {
            var manifest = new BugReportManifest();
            manifest.WebUiHealth = new WebUiHealthReading
            {
                HostStartup = WebUiHostStartupState.SkippedByEditorSwitch,
                CefProcess = CefProcessState.LostRestarting,
                SecondsSinceLastWebMessage = 16.5,
            };

            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual(7, (int)json["schemaVersion"]);
            Assert.AreEqual("skippedByEditorSwitch", (string)json["webUiHealth"]["hostStartup"]);
            Assert.AreEqual("lostRestarting", (string)json["webUiHealth"]["cefProcess"]);
            Assert.AreEqual(16.5, (double)json["webUiHealth"]["secondsSinceLastWebMessage"]);
        }

        // 観測開始は生死を名乗らない綴りで出る（初期化失敗の報告で running と誤報しない）
        // The observation start uses a spelling that claims no liveness (a failed-init report never reads running)
        [Test]
        public void 観測開始はobservedAfterBrowserStartの綴りで出る()
        {
            var manifest = new BugReportManifest();
            manifest.WebUiHealth = new WebUiHealthReading { HostStartup = WebUiHostStartupState.Started, CefProcess = CefProcessState.ObservedAfterBrowserStart };

            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual("observedAfterBrowserStart", (string)json["webUiHealth"]["cefProcess"]);
        }

        // 取れない値は欠損列でなく notAttempted/notObserved/null を名乗る
        // Unavailable values read notAttempted/notObserved/null rather than going to the missing list
        [Test]
        public void 何も記録していなければnotAttemptedとnotObservedとnullで出る()
        {
            var json = JObject.Parse(new BugReportManifest().ToJson());

            Assert.AreEqual("notAttempted", (string)json["webUiHealth"]["hostStartup"]);
            Assert.AreEqual("notObserved", (string)json["webUiHealth"]["cefProcess"]);
            Assert.AreEqual(JTokenType.Null, json["webUiHealth"]["secondsSinceLastWebMessage"].Type);
        }

        // 送信前にWebが復旧しても、箱にはポーズを開いた瞬間（確保時）の値が残る
        // Even if the web recovers before sending, the box keeps the values as of pause-open (capture time)
        [Test]
        public async Task 確保時の値が送信時の値でなく箱のmanifestへ写る()
        {
            var sources = new FakeBugReportCaptureSources { ScreenshotPath = null, Recording = CapturedRecording.Unavailable("テストなので録画しない") };
            sources.WebUiHealthReading = new WebUiHealthReading { HostStartup = WebUiHostStartupState.Failed, CefProcess = CefProcessState.LostRestarting, SecondsSinceLastWebMessage = 40.0 };
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", null, new List<string>(), new List<string>(), null, 0));

            // 確保の後に復旧した値へ変える（送信actionの受信で無応答秒数が0へ戻る状況）
            // Switch to recovered values after the capture, as the send action's receipt resets the silence to zero
            sources.WebUiHealthReading = new WebUiHealthReading { HostStartup = WebUiHostStartupState.Failed, CefProcess = CefProcessState.ObservedAfterBrowserStart, SecondsSinceLastWebMessage = 0.01 };
            var ticket = session.TryBeginSubmit();
            Assert.IsTrue(ticket.Allowed, ticket.RefusedCode);

            var writer = new BugReportBundleWriter(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason), new TestBugReportOutboxRoot());
            var result = await writer.WriteAsync(ticket.Data, "webUiHealth確保時テスト", PlaytestReportKind.Bug);
            try
            {
                Assert.IsTrue(result.Ready, "manifestの書き出しに失敗した");
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(result.BundleDirectory, BugReportBundleLayout.ManifestFileName)));
                Assert.AreEqual("failed", (string)manifest["webUiHealth"]["hostStartup"]);
                Assert.AreEqual("lostRestarting", (string)manifest["webUiHealth"]["cefProcess"]);
                Assert.AreEqual(40.0, (double)manifest["webUiHealth"]["secondsSinceLastWebMessage"]);
            }
            finally
            {
                Directory.Delete(result.BundleDirectory, true);
                BugReportCaptureWorkspace.Delete(ticket.Data.CaptureWorkDirectory);
            }
        }
    }
}
