using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 救出操作（再送・書き出し）の出し分けと配送の行を確かめる（L1。Web の BugReportFormTests/recoveryControls.test.ts・deliveryStatus.test.ts）
    /// Verifies which rescue controls (resend, export) show and the delivery line (L1; Web BugReportFormTests/recoveryControls.test.ts and deliveryStatus.test.ts)
    /// </summary>
    public class BugReportRecoveryControlsViewTest : ScreenTestHost
    {
        private RecordingPauseMenuCommands _commands;
        private BugReportFormView _view;

        [SetUp]
        public void MountForm()
        {
            _commands = new RecordingPauseMenuCommands();
            var initial = new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(BugReportCaptureStatus.Ready), PlaytestDeliveryAllSent.Instance, PlaytestExportIdle.Instance);
            _view = Mount(new BugReportFormView(initial, _commands, Toasts, Toaster, new BugReportDraft()));
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">送信できない報告があるときだけ再送ボタンを出し、押すと action を出す</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">送信待ちだけなら再送は出さず書き出しは出す</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">localOnly では失敗があっても再送は出さず書き出しは出す</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">refused では失敗があっても再送は出さず書き出しは出す</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">すべて送信済みなら両方とも出さない</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">書き出し中はボタンを押せない</web-test>
        [Test]
        public void 救出ボタンは配送状況で出し分け書き出し中は押せない()
        {
            // 失敗があれば再送と書き出しの両方。押すとコマンドを出す
            // With failures both resend and export show, and pressing them issues the commands
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(0, 2)), PlaytestExportIdle.Instance);
            Assert.IsTrue(IsShown(Find<Button>(_view, "bug-report-resend-failed")));
            simulate.Click(Find<Button>(_view, "bug-report-resend-failed"));
            simulate.Click(Find<Button>(_view, "bug-report-export-unsent"));
            CollectionAssert.AreEqual(new[] { PauseMenuCommandKind.ResendFailedReports, PauseMenuCommandKind.ExportUnsentReports }, _commands.Issued.Select(record => record.Kind));
            Assert.AreEqual(BugReportFormText.DescribeDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(0, 2))), Find<Label>(_view, "bug-report-delivery").text);

            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(3, 0)), PlaytestExportIdle.Instance);
            AssertRecovery(false, true);
            ApplyDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenMissing, new PlaytestOutboxDeliveryCounts(1, 1)), PlaytestExportIdle.Instance);
            AssertRecovery(false, true);
            ApplyDelivery(new PlaytestDeliveryRefused(new PlaytestOutboxDeliveryCounts(1, 1)), PlaytestExportIdle.Instance);
            AssertRecovery(false, true);
            ApplyDelivery(PlaytestDeliveryAllSent.Instance, PlaytestExportIdle.Instance);
            AssertRecovery(false, false);
            Assert.IsFalse(IsShown(Find<Label>(_view, "bug-report-export-status")));

            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(0, 2)), PlaytestExportRunning.Instance);
            Assert.IsFalse(Find<Button>(_view, "bug-report-resend-failed").enabledSelf);
            Assert.IsFalse(Find<Button>(_view, "bug-report-export-unsent").enabledSelf);
            Assert.IsTrue(IsShown(Find<Label>(_view, "bug-report-export-status")));
        }

        private void ApplyDelivery(PlaytestDeliveryStatus delivery, PlaytestExportStatus export)
        {
            _view.Apply(new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(BugReportCaptureStatus.Ready), delivery, export));
        }

        private void AssertRecovery(bool resend, bool export)
        {
            Assert.AreEqual(resend, IsShown(Find<Button>(_view, "bug-report-resend-failed")), "resend");
            Assert.AreEqual(export, IsShown(Find<Button>(_view, "bug-report-export-unsent")), "export");
        }
    }
}
