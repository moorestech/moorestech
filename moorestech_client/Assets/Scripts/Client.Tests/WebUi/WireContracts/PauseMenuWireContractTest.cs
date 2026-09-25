using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.WireContracts
{
    // ポーズ/Action応答契約をfixtureと封筒で固定
    // Pins pause-menu/action-response C#/TS contract via fixtures and envelope
    public class PauseMenuWireContractTest
    {
        // 切断表示・確保状態・今の画面を配信
        // Sends disconnect state, capture status, current page
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
