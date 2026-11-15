using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.TestHost;
using Client.Ui.State.PauseMenu;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 配送の行が状態の行と同じ画面に並び、各言語の本番辞書で件数と理由を保つことを確かめる（L1。Web の BugReportFormTests/deliveryStatus.test.ts）
    /// Verifies the delivery line sits beside the status line and keeps counts and reasons in every language's production dictionary (L1; Web BugReportFormTests/deliveryStatus.test.ts)
    /// </summary>
    public class BugReportDeliveryStatusViewTest : BugReportFormScreenTestHost
    {
        private const string UntranslatedKeyPrefix = "ui.bugReport.";

        // 期待は Web と同じく辞書から独立に書いた日本語
        // Expectations are Japanese written independently of the dictionary, as on the Web
        private static IEnumerable<TestCaseData> KnownDeliveries()
        {
            yield return new TestCaseData(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenMissing, Counts(3, 0)), "未送信: 開発者トークンが未設定です（この機材に3件保存）").SetName("配送行と状態行_未送信_トークン未設定");
            yield return new TestCaseData(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, Counts(3, 0)), "未送信: 開発者トークンを読めません（この機材に3件保存）").SetName("配送行と状態行_未送信_トークン読めない");
            yield return new TestCaseData(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DistributionUnavailable, Counts(3, 0)), "未送信: build-infoとSteamの送信条件を確認してください（この機材に3件保存）").SetName("配送行と状態行_未送信_配布条件");
            yield return new TestCaseData(new PlaytestDeliveryPending(Counts(3, 0)), "送信待ち 3件").SetName("配送行と状態行_送信待ち");
            yield return new TestCaseData(new PlaytestDeliveryRefused(Counts(3, 0)), "受け口に拒否されました。トークン失効の可能性があります（送信待ち3件）").SetName("配送行と状態行_拒否");
            yield return new TestCaseData(PlaytestDeliveryAllSent.Instance, "すべての報告を送信しました").SetName("配送行と状態行_全件送信済み");
        }

        private static IEnumerable<string> LanguageCodes()
        {
            return LanguageCatalog.Languages.Select(language => language.Code);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts">送信状況 %j と送信可否を同時に表示する</web-test>
        [TestCaseSource(nameof(KnownDeliveries))]
        public void 配送の行と送れない理由の状態行を同じ画面に同時に出す(PlaytestDeliveryStatus delivery, string expected)
        {
            // 確保セッションが無い状態で配送状況を流す
            // Push the delivery status while there is no capture session
            Assert.IsTrue(Localize.TrySetChosenLanguage("japanese"), "日本語へ切り替えられる");
            Type("broken belt");
            View.Apply(new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(BugReportCaptureStatus.NoSession), delivery, PlaytestExportIdle.Instance));

            // 配送行と状態行が出て送信は不可
            // Delivery and status lines show, send disabled
            var deliveryLine = Find<Label>(View, "bug-report-delivery");
            Assert.IsTrue(IsShown(deliveryLine));
            Assert.AreEqual(expected, deliveryLine.PlainText());
            var statusLines = View.Query<Label>("bug-report-status").ToList();
            Assert.AreEqual(1, statusLines.Count);
            Assert.AreEqual("記録を確保するためポーズメニューを開き直してください", statusLines[0].PlainText());
            Assert.IsFalse(SendButton().enabledSelf);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts">%s辞書で配送不能件数と復旧案内を出す</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で配送不能件数を出し旧い復旧案内を出さない(string languageCode)
        {
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            ApplyDelivery(new PlaytestDeliveryPending(Counts(0, 2)));

            // 失敗件数が残り旧案内は出ない
            // Failed count stays, old guidance absent
            var text = Find<Label>(View, "bug-report-delivery").PlainText();
            StringAssert.Contains("2", text);
            StringAssert.DoesNotContain(UntranslatedKeyPrefix, text);
            StringAssert.DoesNotMatch("再送復旧|recover|Wiederherstellung|재전송 복구", text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts">%s辞書で件数不明を完了や0件にしない</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で件数不明を完了や0件にしない(string languageCode)
        {
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxInventoryUnavailable("access denied")));

            // 理由が残り0件にならない
            // Reason stays, never zero counts
            var text = Find<Label>(View, "bug-report-delivery").PlainText();
            StringAssert.Contains("access denied", text);
            StringAssert.DoesNotContain("0", text);
            StringAssert.DoesNotContain(UntranslatedKeyPrefix, text);
        }

        private void ApplyDelivery(PlaytestDeliveryStatus delivery)
        {
            View.Apply(new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(BugReportCaptureStatus.Ready), delivery, PlaytestExportIdle.Instance));
        }

        private static PlaytestOutboxDeliveryCounts Counts(int pending, int failed)
        {
            return new PlaytestOutboxDeliveryCounts(pending, failed);
        }
    }
}
