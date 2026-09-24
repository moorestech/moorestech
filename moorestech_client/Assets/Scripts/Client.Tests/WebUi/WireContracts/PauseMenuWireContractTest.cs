using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.WireContracts
{
    // ポーズメニューとAction応答のC#⇔TypeScript契約を、共有フィクスチャと応答封筒で固定する
    // Pins the C#-to-TypeScript pause-menu and action-response contracts with shared fixtures and envelopes
    public class PauseMenuWireContractTest
    {
        // ポーズメニューは切断表示・報告の確保状態・今の画面を配信する
        // The pause menu sends the disconnect state, the report capture status and the current page
        [Test]
        public void PauseMenuMatchesFixture()
        {
            var dto = new PauseMenuDto
            {
                Disconnected = true,
                BugReport = new BugReportStatusDto { Kind = BugReportCaptureStatus.Capturing, Missing = new List<string> { "video" } },
                Page = PauseMenuPageContract.ToContractText(PauseMenuPage.BugReport),
            };

            AssertMatchesFixture(dto, "pause_menu.json");
        }

        // Action成功payloadはresult封筒に保持し、送った報告の欠損をtopic更新と分離する
        // Success payload stays in the result envelope, separating sent-report gaps from topic updates
        [Test]
        public void ActionResultEnvelopePreservesPayload()
        {
            var payload = new JObject { ["missing"] = new JArray("video", "serverSnapshot") };
            var actual = JObject.Parse(WebSocketEnvelope.BuildResult("request-1", true, null, payload));

            Assert.IsTrue(JToken.DeepEquals(payload, actual["payload"]));
        }

        // バグ報告の成功応答を共有fixtureに固定し、Web側スキーマとの片側変更を検出する
        // Pins the successful bug-report response to the shared fixture so one-sided Web schema changes are detected
        [Test]
        public void BugReportSubmitResultMatchesFixture()
        {
            var submitted = BugReportSubmitResult.Succeed("/outbox/report", new List<MissingItem>
            {
                new() { Item = "video", Reason = "録画なし" },
                new() { Item = "serverSnapshot", Reason = "取得失敗" },
            });

            var result = BugReportSubmitActionHandler.CreateSuccessResult(submitted);

            AssertMatchesFixture(result.Payload, "bug_report_submit_result.json");
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var expected = JToken.Parse(LoadFixture(fixtureName));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"{fixtureName} mismatch\nexpected: {expected}\nactual:   {actual}");
        }

        private static string LoadFixture(string fixtureName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName));
        }
    }
}
