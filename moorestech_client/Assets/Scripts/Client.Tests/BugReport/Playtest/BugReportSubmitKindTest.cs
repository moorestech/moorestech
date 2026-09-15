using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Tests.Playtest;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BugReportSubmitKindTest
    {
        [Test]
        public void manifestにkindが出る()
        {
            var manifest = new BugReportManifest
            {
                CreatedAt = "2026-09-13T12:00:00Z",
                Description = "説明",
                Platform = "OSXEditor",
                IsEditor = true,
                Kind = PlaytestReportKindText.ToContractText(PlaytestReportKind.Feedback),
                SnapshotTicks = new List<ulong>(),
                SnapshotFiles = new List<string>(),
                PacketLogFiles = new List<string>(),
                Missing = new List<MissingItem>(),
            };
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual("feedback", (string)json["kind"]);
        }

        [Test]
        public void 範囲外のkindはactionが拒否する()
        {
            // kind 検証は確保セッション解決より前段なので、UIStateControlはnullのままでも足りる
            // The kind check runs before the capture session is resolved, so UIStateControl may stay null here
            var session = new BugReportCaptureSession(new NullBugReportCaptureSources());
            var handler = new BugReportSubmitActionHandler(new BugReportBundleWriter(new EmptyPlaytestSessionIdentity()), session, null, new RecordingProgressSink());

            var crash = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "crash" }).GetAwaiter().GetResult();
            Assert.IsFalse(crash.Ok);
            Assert.AreEqual("invalid_kind", crash.Error);

            var unknown = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "nope" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", unknown.Error);

            var missing = handler.ExecuteAsync(new JObject { ["description"] = "説明" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", missing.Error);
        }
    }
}
