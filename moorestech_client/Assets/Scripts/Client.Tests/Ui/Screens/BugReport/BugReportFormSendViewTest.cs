using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.TestHost;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 送信の流れ・トースト・下書き・種別文言（L1）
    /// Send flow, toasts, draft, kind labels (L1)
    /// </summary>
    public class BugReportFormSendViewTest : BugReportFormScreenTestHost
    {

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">入力後の送信で bug_report.submit を説明文付きで送りトーストを出す</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">送信後に画面遷移のactionは出さない</web-test>
        [Test]
        public void 入力後の送信で説明文付きで送り成功のトーストを出し画面遷移は出さない()
        {
            Type("  broken belt  ");
            simulate.Click(SendButton());
            Commands.CompleteSubmit(UiCommandAccepted.Instance);

            var submit = Commands.Issued.Single();
            Assert.AreEqual(PauseMenuCommandKind.SubmitBugReport, submit.Kind);
            Assert.AreEqual("broken belt", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Bug, submit.ReportKind);
            Assert.AreEqual(GameUiToastVariant.Info, ShownToasts.Single().Variant);
            Assert.AreEqual(new UiText(BugReportFormText.SentToast(PlaytestReportKind.Bug)).Resolve(), ShownToasts.Single().Text);
            Assert.AreEqual("", Draft.Description);
            Assert.AreEqual("", Description().value);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">送信中は種別切替と記述欄を操作できなくする</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">書き出し中は二度目の送信を受け付けない</web-test>
        [Test]
        public void 送信中は二度目の送信を受け付けず種別と記述欄を操作できない()
        {
            Type("broken belt");
            simulate.Click(SendButton());
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.IsFalse(Description().enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-feedback").enabledSelf);
            simulate.Click(SendButton());
            Assert.AreEqual(1, Commands.Issued.Count);

            Commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.IsTrue(Description().enabledSelf);
        }

        [Test]
        public void 送信中に下書きが作り直されたら古い送信の完了は新しい下書きを消さない()
        {
            Type("report A");
            simulate.Click(SendButton());

            // 送信中にポーズを開き直して別の本文を書き始めた状態
            // The pause was reopened during the send and a different description was started
            Draft.Reset();
            Draft.SetDescription("report B");

            LogAssert.Expect(LogType.Warning, "[BugReportFormView] draft was reset during the send; keeping the newer draft");
            Commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual("report B", Draft.Description);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">確保済みの欠損を成功トーストに出す</web-test>
        [Test]
        public void 確保済みの欠損は送信成功時にエラーのトーストで出す()
        {
            View.Apply(Model(BugReportCaptureStatus.Ready, "video"));
            Type("broken belt");
            simulate.FrameUpdate();
            simulate.Click(SendButton());

            // 送信中に欠損が消えても押した時点の欠損で伝える
            // Even if the gap clears during the send, report the gap seen at click time
            View.Apply(Model(BugReportCaptureStatus.Ready));
            Commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
            StringAssert.Contains("video", ShownToasts.Single().Text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">送信成功後は種別が既定のバグへ戻る</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">感想へ切り替えるとkindがfeedbackになる</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想のボタンと成功通知を選び、バグのボタンへ戻せる</web-test>
        [Test]
        public void 感想へ切り替えると種別と文言が感想になり送信成功後はバグへ戻る()
        {
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Assert.AreEqual(PlaytestReportKind.Feedback, Draft.Kind);
            Assert.AreEqual("Send impressions", SendButton().Q<Label>().text);
            Type("nice");
            simulate.Click(SendButton());
            Assert.AreEqual(PlaytestReportKind.Feedback, Commands.Issued.Single().ReportKind);
            StringAssert.Contains("impressions", Status().text);

            Commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual(new UiText(BugReportFormText.SentToast(PlaytestReportKind.Feedback)).Resolve(), ShownToasts.Single().Text);
            Assert.AreEqual(PlaytestReportKind.Bug, Draft.Kind);
            Assert.AreEqual("Send bug report", SendButton().Q<Label>().text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想送信が失敗すると成功通知を出さずdraftを残す</web-test>
        [Test]
        public void 送信が拒否されると成功通知を出さず下書きを残す()
        {
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("nice");
            simulate.Click(SendButton());
            Commands.CompleteSubmit(UiCommandRejected.Of(UiCommandRejectReason.CaptureRefused));

            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.CommandRejected.CaptureRefused), ShownToasts.Single().Text, "トーストに未翻訳の失敗コードが出た");
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
            Assert.AreEqual("nice", Draft.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, Draft.Kind);
            Assert.IsTrue(SendButton().enabledSelf);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想でも欠損時は従来の通知を出す</web-test>
        [Test]
        public void 感想でも欠損があれば送信成功時に欠損のエラーのトーストを出す()
        {
            // 欠損ありで感想を送る。状態行が増えて送信ボタンがずれるので 1 フレーム進めてから押す
            // Send impressions with a gap; the extra status line shifts the send button, so advance a frame first
            View.Apply(Model(BugReportCaptureStatus.Ready, "video"));
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("感想");
            simulate.FrameUpdate();
            simulate.Click(SendButton());
            Commands.CompleteSubmit(UiCommandAccepted.Instance);

            // 感想の完了ではなく欠損のエラーを 1 件出す
            // Show one gap error instead of the impressions completion
            Assert.AreEqual(PlaytestReportKind.Feedback, Commands.Issued.Single().ReportKind);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
            Assert.AreEqual("Missing: video", ShownToasts.Single().Text);
            Assert.AreNotEqual(Localize.Get(LocalizationKeys.Ui.FeedbackReport.Sent), ShownToasts.Single().Text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想送信中の表示とロックを保ち、送信開始時の種別で完了を伝える</web-test>
        [Test]
        public void 感想の送信中は表示とロックを保ち完了は送信開始時の種別で伝える()
        {
            // 感想を送ると、応答待ちの間は感想の書き出し中の表示とロックが出る
            // Sending impressions shows the impressions writing label and the lock while awaiting the reply
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("感想");
            simulate.Click(SendButton());
            Assert.AreEqual("Writing the impressions…", Status().PlainText());
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-feedback").enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-bug").enabledSelf);

            // 応答待ちの間に下書きの種別がバグへ変わっても、完了は感想として伝える
            // Even if the draft kind turns to bug while awaiting, completion is reported as impressions
            Draft.SetKind(PlaytestReportKind.Bug);
            Commands.CompleteSubmit(UiCommandAccepted.Instance);
            var submit = Commands.Issued.Single();
            Assert.AreEqual("感想", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, submit.ReportKind);
            Assert.AreEqual(GameUiToastVariant.Info, ShownToasts.Single().Variant);
            Assert.AreEqual("Impressions saved to outbox", ShownToasts.Single().Text);
        }
    }
}
