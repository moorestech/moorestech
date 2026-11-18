using System;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State
{
    public class BugReportFormTextTest
    {
        private static readonly string[] NoMissing = Array.Empty<string>();

        [SetUp]
        public void SetUpJapanese()
        {
            Localize.Initialize();
            Localize.TrySetChosenLanguage("japanese");
        }

        [TearDown]
        public void TearDownLanguage()
        {
            Localize.Initialize();
        }

        private static BugReportCaptureStatus Capture(string kind, params string[] missing) => new BugReportCaptureStatus(kind, missing);
        private static PlaytestOutboxDeliveryCounts Counts(int pending, int failed) => new PlaytestOutboxDeliveryCounts(pending, failed);
        private static PlaytestOutboxInventoryUnavailable Unavailable() => new PlaytestOutboxInventoryUnavailable("access denied");

        private static void AssertText(LocalizationKey expectedKey, string[] expectedParams, UiText actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expectedKey.Key, actual.Key.Key);
            Assert.AreEqual(expectedParams.Length / 2, actual.Params.Count);
            for (var i = 0; i < actual.Params.Count; i++)
            {
                Assert.AreEqual(expectedParams[i * 2], actual.Params[i].Name);
                Assert.AreEqual(expectedParams[i * 2 + 1], actual.Params[i].Value);
            }
        }

        [Test]
        public void 確保セッションが無いときはnoSessionの文を出す()
        {
            AssertText(LocalizationKeys.Ui.BugReport.NoSession, new string[0], BugReportFormText.DescribeStatus(Capture(BugReportCaptureStatus.NoSession), false, PlaytestReportKind.Bug));
        }

        [Test]
        public void 確保中は確保中の文を出す()
        {
            AssertText(LocalizationKeys.Ui.BugReport.CapturePending, new string[0], BugReportFormText.DescribeStatus(Capture(BugReportCaptureStatus.Capturing), false, PlaytestReportKind.Bug));
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">ローカル送信前でもホスト送信中は感想の表示を使う</web-test>
        [Test]
        public void ホスト送信中は種別の書き出し中の文を出す()
        {
            var submitting = Capture(BugReportCaptureStatus.Submitting);
            AssertText(LocalizationKeys.Ui.BugReport.Sending, new string[0], BugReportFormText.DescribeStatus(submitting, false, PlaytestReportKind.Bug));
            AssertText(LocalizationKeys.Ui.FeedbackReport.Sending, new string[0], BugReportFormText.DescribeStatus(submitting, false, PlaytestReportKind.Feedback));
        }

        [Test]
        public void 押下中は確保状態に関わらず種別の書き出し中の文を出す()
        {
            AssertText(LocalizationKeys.Ui.FeedbackReport.Sending, new string[0], BugReportFormText.DescribeStatus(Capture(BugReportCaptureStatus.Ready), true, PlaytestReportKind.Feedback));
        }

        [Test]
        public void 送れる状態では状態の文は出さず欠損が無ければ欠損も出さない()
        {
            var ready = Capture(BugReportCaptureStatus.Ready);
            Assert.IsNull(BugReportFormText.DescribeStatus(ready, false, PlaytestReportKind.Bug));
            Assert.IsNull(BugReportFormText.DescribeMissing(ready));
        }

        [Test]
        public void 送れる状態でも欠損があれば項目をカンマ区切りで出す()
        {
            var ready = Capture(BugReportCaptureStatus.Ready, "screenshot", "video");
            AssertText(LocalizationKeys.Ui.BugReport.Missing, new[] { "items", "screenshot, video" }, BugReportFormText.DescribeMissing(ready));
        }

        [Test]
        public void 送信ボタンと完了通知は種別ごとのキーを返す()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BugReport.Send.Key, BugReportFormText.SendLabel(PlaytestReportKind.Bug).Key);
            Assert.AreEqual(LocalizationKeys.Ui.FeedbackReport.Send.Key, BugReportFormText.SendLabel(PlaytestReportKind.Feedback).Key);
            Assert.AreEqual(LocalizationKeys.Ui.BugReport.Sent.Key, BugReportFormText.SentToast(PlaytestReportKind.Bug).Key);
            Assert.AreEqual(LocalizationKeys.Ui.FeedbackReport.Sent.Key, BugReportFormText.SentToast(PlaytestReportKind.Feedback).Key);
        }

        [Test]
        public void 件数既知の配送状況を種別ごとの文にする()
        {
            Assert.AreEqual("未送信: 開発者トークンが未設定です（この機材に3件保存）", BugReportFormText.DescribeDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenMissing, Counts(3, 0))).Resolve());
            Assert.AreEqual("未送信: 開発者トークンを読めません（この機材に3件保存）", BugReportFormText.DescribeDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, Counts(3, 0))).Resolve());
            Assert.AreEqual("未送信: build-infoとSteamの送信条件を確認してください（この機材に3件保存）", BugReportFormText.DescribeDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DistributionUnavailable, Counts(3, 0))).Resolve());
            Assert.AreEqual("送信待ち 3件", BugReportFormText.DescribeDelivery(new PlaytestDeliveryPending(Counts(3, 0))).Resolve());
            Assert.AreEqual("受け口に拒否されました。トークン失効の可能性があります（送信待ち3件）", BugReportFormText.DescribeDelivery(new PlaytestDeliveryRefused(Counts(3, 0))).Resolve());
            Assert.AreEqual("すべての報告を送信しました", BugReportFormText.DescribeDelivery(PlaytestDeliveryAllSent.Instance).Resolve());
        }

        [Test]
        public void 失敗件数があれば配送の行へ追記する()
        {
            Assert.AreEqual("送信待ち 0件 / 送信できない報告 2件", BugReportFormText.DescribeDelivery(new PlaytestDeliveryPending(Counts(0, 2))).Resolve());
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts">件数不明でも配送理由 %j を保持する</web-test>
        [Test]
        public void 件数不明でも配送理由を保ち完了や0件にしない()
        {
            const string inventory = "配送保留: 保存済み報告を読み取れません（access denied）";
            Assert.AreEqual(inventory, BugReportFormText.DescribeDelivery(new PlaytestDeliveryPending(Unavailable())).Resolve());
            Assert.AreEqual("受け口に拒否されました。トークン失効の可能性があります / " + inventory, BugReportFormText.DescribeDelivery(new PlaytestDeliveryRefused(Unavailable())).Resolve());
            Assert.AreEqual("未送信: 開発者トークンが未設定です / " + inventory, BugReportFormText.DescribeDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenMissing, Unavailable())).Resolve());
            Assert.AreEqual("未送信: 開発者トークンを読めません / " + inventory, BugReportFormText.DescribeDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, Unavailable())).Resolve());
            Assert.AreEqual("未送信: build-infoとSteamの送信条件を確認してください / " + inventory, BugReportFormText.DescribeDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DistributionUnavailable, Unavailable())).Resolve());
        }

        [Test]
        public void 送れなかった報告があるときだけ再送を出す()
        {
            Assert.IsTrue(BugReportFormText.ShowResend(new PlaytestDeliveryPending(Counts(1, 16))));
            Assert.IsFalse(BugReportFormText.ShowResend(new PlaytestDeliveryPending(Counts(2, 0))));
            Assert.IsFalse(BugReportFormText.ShowResend(new PlaytestDeliveryPending(Unavailable())));
            Assert.IsFalse(BugReportFormText.ShowResend(PlaytestDeliveryAllSent.Instance));
        }

        [Test]
        public void localOnlyとrefusedでは失敗があっても再送を出さず書き出しは出す()
        {
            var localOnly = new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DistributionUnavailable, Counts(1, 3));
            var refused = new PlaytestDeliveryRefused(Counts(0, 2));
            Assert.IsFalse(BugReportFormText.ShowResend(localOnly));
            Assert.IsTrue(BugReportFormText.ShowExport(localOnly));
            Assert.IsFalse(BugReportFormText.ShowResend(refused));
            Assert.IsTrue(BugReportFormText.ShowExport(refused));
        }

        [Test]
        public void 件数不明でも書き出しは出し全件送信済みなら出さない()
        {
            Assert.IsTrue(BugReportFormText.ShowExport(new PlaytestDeliveryPending(Unavailable())));
            Assert.IsFalse(BugReportFormText.ShowExport(PlaytestDeliveryAllSent.Instance));
            Assert.IsFalse(BugReportFormText.ShowExport(new PlaytestDeliveryPending(Counts(0, 0))));
            Assert.IsTrue(BugReportFormText.ShowExport(new PlaytestDeliveryPending(Counts(2, 0))));
        }

        [Test]
        public void 書き出しの進行を文にしidleは出さない()
        {
            Assert.IsFalse(BugReportFormText.TryDescribeExport(PlaytestExportIdle.Instance, out var idleLine));
            Assert.IsNull(idleLine);
            Assert.AreEqual("報告を書き出しています…", ExportLine(PlaytestExportRunning.Instance).Resolve());
            Assert.AreEqual("書き出せませんでした: desktop folder unavailable", ExportLine(new PlaytestExportFailed("desktop folder unavailable")).Resolve());
        }

        [Test]
        public void 書き出し完了は欠落と開けなかった案内を同じ行へ連結する()
        {
            var missing = new[] { new PlaytestExportMissing("reports/x/video.mp4", "IOException") };
            var done = new PlaytestExportDone("/exports/unsent-reports.zip", 17, missing, false);
            Assert.AreEqual(
                "17件を /exports/unsent-reports.zip に書き出しました。Googleドライブ等に上げて、共有リンクを開発者へ送ってください"
                + " / 1件は入れられませんでした: reports/x/video.mp4 (IOException) / 上のフォルダを手動で開いてください",
                ExportLine(done).Resolve());
            var clean = new PlaytestExportDone("/z.zip", 1, Array.Empty<PlaytestExportMissing>(), true);
            Assert.AreEqual("1件を /z.zip に書き出しました。Googleドライブ等に上げて、共有リンクを開発者へ送ってください", ExportLine(clean).Resolve());
        }

        [Test]
        public void 配送行と書き出し行は言語を切り替えると新しい言語の文になる()
        {
            var delivery = BugReportFormText.DescribeDelivery(new PlaytestDeliveryPending(Counts(0, 2)));
            var export = ExportLine(new PlaytestExportDone("/z.zip", 1, Array.Empty<PlaytestExportMissing>(), false));
            var japaneseDelivery = delivery.Resolve();
            var japaneseExport = export.Resolve();

            Localize.TrySetChosenLanguage("english");

            Assert.AreNotEqual(japaneseDelivery, delivery.Resolve());
            Assert.AreNotEqual(japaneseExport, export.Resolve());
            Assert.IsTrue(delivery.Resolve().Contains(" / "));
        }

        private static UiText ExportLine(PlaytestExportStatus export)
        {
            Assert.IsTrue(BugReportFormText.TryDescribeExport(export, out var line));
            return line;
        }
    }
}
