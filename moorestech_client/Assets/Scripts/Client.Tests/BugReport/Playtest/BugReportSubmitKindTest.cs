using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Playtest;
using Client.Tests.PlaytestReceiver;
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
            // kind 検証は送信より前段なので、トップへの遷移は起きない
            // The kind check runs before submitting, so no move to the top happens
            var session = new BugReportCaptureSession(new NullBugReportCaptureSources());
            var handler = new BugReportSubmitActionHandler(new BugReportSubmitter(new BugReportBundleWriter(new EmptyPlaytestSessionIdentity()), session, new RecordingProgressSink(), new RecordingUploadRequester()), new PauseMenuStateService());

            var crash = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "crash" }).GetAwaiter().GetResult();
            Assert.IsFalse(crash.Ok);
            Assert.AreEqual("invalid_kind", crash.Error);

            var unknown = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "nope" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", unknown.Error);

            var missing = handler.ExecuteAsync(new JObject { ["description"] = "説明" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", missing.Error);
        }

        // 次の記録を確保し直しても、送った箱の欠損はaction応答に残す
        // Keep the sent bundle's gaps in the action response even after recapturing the next records
        [Test]
        public void 欠損付き成功は送った報告の欠損をpayloadで返す()
        {
            var submitted = BugReportSubmitResult.Succeed("/outbox/report", new List<MissingItem>
            {
                new() { Item = "video", Reason = "ffmpegが見つからなかった" },
                new() { Item = "steamId", Reason = "Steam IDを取得できなかった" },
            });

            var result = BugReportSubmitActionHandler.CreateSuccessResult(submitted);

            Assert.IsTrue(result.Ok);
            CollectionAssert.AreEqual(new[] { "video", "steamId" }, result.Payload["missing"].Values<string>());
        }
    }
}
