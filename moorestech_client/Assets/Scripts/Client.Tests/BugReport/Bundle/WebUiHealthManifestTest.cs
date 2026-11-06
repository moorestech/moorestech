using System.IO;
using System.Threading.Tasks;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.WebUiHealth;
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
        public void 偽のprobeの値がcamelCaseの綴りでmanifestへ出る()
        {
            var manifest = new BugReportManifest();
            manifest.WebUiHealth = new FixedWebUiHealthProbe(new WebUiHealthReading
            {
                HostStartup = WebUiHostStartupState.SkippedByEditorSwitch,
                CefProcess = CefProcessState.LostRestarting,
                SecondsSinceLastWebMessage = 16.5,
            }).Read();

            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual(6, (int)json["schemaVersion"]);
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

        // probe→manifestの配線を確認
        // Verifies probe-to-manifest wiring
        [Test]
        public async Task WriteAsync経由でprobeの値が箱のmanifestへ渡る()
        {
            var probe = new FixedWebUiHealthProbe(new WebUiHealthReading { HostStartup = WebUiHostStartupState.Failed, CefProcess = CefProcessState.RecoveryFailed, SecondsSinceLastWebMessage = 3.0 });
            var writer = new BugReportBundleWriter(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason), probe);
            var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0, Missing = new() };

            var result = await writer.WriteAsync(data, "webUiHealth配線テスト", PlaytestReportKind.Bug);
            try
            {
                Assert.IsTrue(result.Ready, "manifestの書き出しに失敗した");
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(result.BundleDirectory, BugReportBundleLayout.ManifestFileName)));
                Assert.AreEqual("failed", (string)manifest["webUiHealth"]["hostStartup"]);
                Assert.AreEqual("recoveryFailed", (string)manifest["webUiHealth"]["cefProcess"]);
                Assert.AreEqual(3.0, (double)manifest["webUiHealth"]["secondsSinceLastWebMessage"]);
            }
            finally
            {
                Directory.Delete(result.BundleDirectory, true);
            }
        }
    }
}
