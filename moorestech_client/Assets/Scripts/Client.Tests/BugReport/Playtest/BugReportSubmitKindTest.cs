using Client.Tests.Ui.State;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.Commands;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Client.Tests.Ui.State.Toast;

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
        public void 空白本文でも種別を先に解く()
        {
            // 送れない種別なら本文が空でも種別で拒否し、送れる種別なら空の本文で拒否する
            // An unsubmittable kind is refused by kind even with a blank description; a submittable kind is refused for the blank description
            var commands = PauseMenuCommandsTestFactory.Create(new PauseMenuStateService(), new RecordingUiCommandResultToaster());

            var crash = (UiCommandRejected)commands.SubmitBugReport("  ", PlaytestReportKind.Crash).GetAwaiter().GetResult();
            Assert.AreEqual(UiCommandRejectReason.UnsubmittableReportKind, crash.Reason);

            var blank = (UiCommandRejected)commands.SubmitBugReport("  ", PlaytestReportKind.Bug).GetAwaiter().GetResult();
            Assert.AreEqual(UiCommandRejectReason.EmptyDescription, blank.Reason);
        }
    }
}
