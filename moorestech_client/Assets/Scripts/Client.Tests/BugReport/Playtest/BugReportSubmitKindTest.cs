using Client.Tests.Ui.State;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
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
            var handler = new BugReportSubmitActionHandler(PauseMenuCommandsTestFactory.Create(new PauseMenuStateService()));

            var crash = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "crash" }).GetAwaiter().GetResult();
            Assert.IsFalse(crash.Ok);
            Assert.AreEqual("invalid_kind", crash.Error);

            var unknown = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "nope" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", unknown.Error);

            var missing = handler.ExecuteAsync(new JObject { ["description"] = "説明" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", missing.Error);
        }

        [Test]
        public void 空白本文でも種別を先に解く()
        {
            // 種別が不正なら本文が空でも invalid_kind、種別が正しければコマンドが empty_description で拒否する
            // An invalid kind yields invalid_kind even with a blank description; a valid kind lets the command refuse as empty_description
            var handler = new BugReportSubmitActionHandler(PauseMenuCommandsTestFactory.Create(new PauseMenuStateService()));

            var invalidKind = handler.ExecuteAsync(new JObject { ["description"] = "  ", ["kind"] = "crash" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", invalidKind.Error);

            var blank = handler.ExecuteAsync(new JObject { ["description"] = "  ", ["kind"] = "bug" }).GetAwaiter().GetResult();
            Assert.IsFalse(blank.Ok);
            Assert.AreEqual("empty_description", blank.Error);
        }
    }
}
