using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 救出操作（再送・書き出し）の出し分けと配送の行を確かめる（L1。Web の BugReportFormTests/recoveryControls.test.ts・deliveryStatus.test.ts）
    /// Verifies which rescue controls (resend, export) show and the delivery line (L1; Web BugReportFormTests/recoveryControls.test.ts and deliveryStatus.test.ts)
    /// </summary>
    public class BugReportRecoveryControlsViewTest : BugReportFormScreenTestHost
    {
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
            Assert.IsTrue(IsShown(Find<Button>(View, "bug-report-resend-failed")));
            simulate.Click(Find<Button>(View, "bug-report-resend-failed"));
            simulate.Click(Find<Button>(View, "bug-report-export-unsent"));
            CollectionAssert.AreEqual(new[] { PauseMenuCommandKind.ResendFailedReports, PauseMenuCommandKind.ExportUnsentReports }, Commands.Issued.Select(record => record.Kind));
            Assert.AreEqual(BugReportFormText.DescribeDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(0, 2))).Resolve(), Find<Label>(View, "bug-report-delivery").text);

            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(3, 0)), PlaytestExportIdle.Instance);
            AssertRecovery(false, true);
            ApplyDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenMissing, new PlaytestOutboxDeliveryCounts(1, 1)), PlaytestExportIdle.Instance);
            AssertRecovery(false, true);
            ApplyDelivery(new PlaytestDeliveryRefused(new PlaytestOutboxDeliveryCounts(1, 1)), PlaytestExportIdle.Instance);
            AssertRecovery(false, true);
            ApplyDelivery(PlaytestDeliveryAllSent.Instance, PlaytestExportIdle.Instance);
            AssertRecovery(false, false);
            Assert.IsFalse(IsShown(Find<Label>(View, "bug-report-export-status")));

            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(0, 2)), PlaytestExportRunning.Instance);
            Assert.IsFalse(Find<Button>(View, "bug-report-resend-failed").enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-export-unsent").enabledSelf);
            Assert.IsTrue(IsShown(Find<Label>(View, "bug-report-export-status")));
        }

        private static IEnumerable<string> LanguageCodes()
        {
            return LanguageCatalog.Languages.Select(language => language.Code);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">%s辞書でボタン2つと書き出し中の文言が未翻訳キーにならない</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で救出ボタン2つと書き出し中の文言が未翻訳キーにならない(string languageCode)
        {
            // 失敗ありの書き出し中で、再送・書き出し・書き出し状況の 3 つを出す
            // Exporting with failures shows all three: resend, export and the export status
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(1, 16)), PlaytestExportRunning.Instance);
            var texts = new[]
            {
                Find<FormButton>(View, "bug-report-resend-failed").Q<Label>().PlainText(),
                Find<FormButton>(View, "bug-report-export-unsent").Q<Label>().PlainText(),
                Find<Label>(View, "bug-report-export-status").PlainText(),
            };

            // どれも空でなく未翻訳キーでもない
            // None is empty or an untranslated key
            foreach (var text in texts)
            {
                Assert.IsNotEmpty(text, languageCode);
                StringAssert.DoesNotContain("ui.bugReport.", text, languageCode);
            }
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">%s辞書で完了・欠落・開けなかった案内・失敗の文言が未翻訳キーにならない</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で書き出しの完了と失敗の文言が差し込み値を保ち未翻訳キーにならない(string languageCode)
        {
            // 完了・欠落・開けなかった案内を 1 行にした文に、差し込み値が全部残る
            // The done, missing and not-revealed sentence keeps every inserted value
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            var missing = new[] { new PlaytestExportMissing("reports/x/video.mp4", "IOException") };
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(1, 16)), new PlaytestExportDone("/exports/unsent-reports.zip", 17, missing, false));
            var done = Find<Label>(View, "bug-report-export-status").PlainText();
            StringAssert.Contains("/exports/unsent-reports.zip", done);
            StringAssert.Contains("17", done);
            StringAssert.Contains("reports/x/video.mp4", done);
            StringAssert.Contains("IOException", done);
            StringAssert.DoesNotContain("ui.bugReport.", done);

            // 失敗の文に理由が残る
            // The failure sentence keeps its reason
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(1, 16)), new PlaytestExportFailed("desktop folder unavailable"));
            var failed = Find<Label>(View, "bug-report-export-status").PlainText();
            StringAssert.Contains("desktop folder unavailable", failed);
            StringAssert.DoesNotContain("ui.bugReport.", failed);
        }

        private void ApplyDelivery(PlaytestDeliveryStatus delivery, PlaytestExportStatus export)
        {
            View.Apply(new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(BugReportCaptureStatus.Ready), delivery, export));
        }

        private void AssertRecovery(bool resend, bool export)
        {
            Assert.AreEqual(resend, IsShown(Find<Button>(View, "bug-report-resend-failed")), "resend");
            Assert.AreEqual(export, IsShown(Find<Button>(View, "bug-report-export-unsent")), "export");
        }
    }
}
