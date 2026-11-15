using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.TestHost;
using Client.Ui.State.Commands;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 記述欄・送信可否・状態行・送信中ロック（L1）
    /// Description, send availability, status line, submit lock (L1)
    /// </summary>
    public class BugReportFormViewTest : BugReportFormScreenTestHost
    {
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">記述欄と送信ボタンを描く</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">空文字では送信ボタンを無効にして送らない</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">空白だけの記述も無効として扱う</web-test>
        [Test]
        public void 記述欄と送信ボタンを描き空文字と空白だけでは送れない()
        {
            Assert.IsTrue(Description().multiline);
            Assert.AreEqual("Send bug report", SendButton().Q<Label>().text);
            Assert.IsFalse(SendButton().enabledSelf);
            Type("   \n ");
            Assert.IsFalse(SendButton().enabledSelf);
            Type("broken belt");
            Assert.IsTrue(SendButton().enabledSelf);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">確保中は送信ボタンを押せない</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">状態行は1本だけ描き、確保中は確保中の文言を出す</web-test>
        [Test]
        public void 確保中は送信ボタンを押せず状態行は確保中の1本だけ()
        {
            Type("broken belt");
            View.Apply(Model(BugReportCaptureStatus.Capturing, "video"));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.AreEqual(1, View.Query<Label>("bug-report-status").ToList().Count);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BugReport.CapturePending).Resolve(), Status().text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">確保セッションが無いときは押せず理由を出す</web-test>
        [Test]
        public void 確保セッションが無いときは押せず理由を出す()
        {
            Type("broken belt");
            View.Apply(Model(BugReportCaptureStatus.NoSession));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.IsNotNull(Status());
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">送れる状態なら欠損の一覧を同じ1本の行で出す</web-test>
        [Test]
        public void 送れる状態なら欠損の一覧を同じ1本の行で出す()
        {
            Assert.IsNull(View.Q<Label>("bug-report-status"));
            View.Apply(Model(BugReportCaptureStatus.Ready, "video", "logs"));
            StringAssert.Contains("video, logs", Status().text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/hostSubmissionLock.test.ts">応答待ちが終了してもホスト送信中は感想の種別と本文を保護する</web-test>
        [Test]
        public void 応答待ちが終わってもホスト送信中は種別と本文を保護しreadyで解く()
        {
            // 感想を送り、ローカルの応答より先にホストが送信中になる
            // Send impressions; the host turns to submitting before the local reply returns
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("感想");
            simulate.Click(SendButton());
            View.Apply(Model(BugReportCaptureStatus.Submitting));

            // ローカルの応答が失敗で終わっても、ホスト送信中のロックと文言は残る
            // Even after the local reply ends in failure, the host-submitting lock and label remain
            Commands.CompleteSubmit(UiCommandRejected.Of(UiCommandRejectReason.CaptureRefused));
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-bug").enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-feedback").enabledSelf);
            Assert.IsFalse(Description().enabledSelf);
            Assert.IsFalse(SendButton().enabledSelf);
            var statusLines = View.Query<Label>("bug-report-status").ToList();
            Assert.AreEqual(1, statusLines.Count);
            Assert.AreEqual("Writing the impressions…", statusLines[0].PlainText());

            // 送った中身は感想で、成功のトーストは出ない
            // The payload was impressions and no success toast shows
            var submit = Commands.Issued.Single();
            Assert.AreEqual(PauseMenuCommandKind.SubmitBugReport, submit.Kind);
            Assert.AreEqual("感想", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, submit.ReportKind);
            Assert.IsFalse(ShownToasts.Any(toast => toast.Variant == GameUiToastVariant.Info), "ホスト送信中に成功のトーストを出した");

            // ホストの送信が終われば書きかけを編集できる
            // Editing resumes once the host finishes writing
            View.Apply(Model(BugReportCaptureStatus.Ready));
            Assert.IsTrue(Find<Button>(View, "bug-report-kind-bug").enabledSelf);
            Assert.IsTrue(Description().enabledSelf);
            Assert.IsTrue(SendButton().enabledSelf);
            Assert.AreEqual("感想", Draft.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, Draft.Kind);
        }

        [Test]
        public void 見出しと記述欄と状態行とボタンの縦の間はMantineのspacing_mdの16px()
        {
            // 未解決の var() は UITK が黙って 0 にするので、4 か所が --mantine-spacing-md の 16px に解決されることを見る
            // UITK silently turns an unresolved var() into 0, so check all four resolve to the 16px of --mantine-spacing-md
            const float mantineSpacingMd = 16f;
            View.Apply(Model(BugReportCaptureStatus.Ready, "video"));
            simulate.FrameUpdate();
            Assert.AreEqual(mantineSpacingMd, View.Q<Label>(className: "bug-report__field-label").resolvedStyle.marginBottom, 0.01f);
            Assert.AreEqual(mantineSpacingMd, Description().parent.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(mantineSpacingMd, Status().resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(mantineSpacingMd, SendButton().resolvedStyle.marginTop, 0.01f);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/productionSendingLabels.test.ts">${locale}: ${kind}のホスト送信表示は本番CSVを使う</web-test>
        [TestCase("english", PlaytestReportKind.Feedback, "Writing the impressions…")]
        [TestCase("english", PlaytestReportKind.Bug, "Writing the bug report…")]
        [TestCase("japanese", PlaytestReportKind.Feedback, "感想を書き出しています…")]
        [TestCase("japanese", PlaytestReportKind.Bug, "バグ報告を書き出しています…")]
        [TestCase("german", PlaytestReportKind.Feedback, "Eindrücke werden geschrieben…")]
        [TestCase("german", PlaytestReportKind.Bug, "Fehlerbericht wird geschrieben…")]
        [TestCase("korean", PlaytestReportKind.Feedback, "감상을 작성하는 중…")]
        [TestCase("korean", PlaytestReportKind.Bug, "버그 신고를 작성하는 중…")]
        public void ホスト送信中の状態行は本番辞書の各言語で種別の文言を出す(string locale, PlaytestReportKind kind, string expected)
        {
            // 辞書を切り替えてからホスト送信中へ移る（Web と同じく辞書の差し替え後の状態遷移を見る）
            // Switch the dictionary, then move to host submitting (the transition after a dictionary swap, as on the Web)
            Draft.SetKind(kind);
            Assert.IsTrue(Localize.TrySetChosenLanguage(locale), $"言語 {locale} へ切り替えられる");
            View.Apply(Model(BugReportCaptureStatus.Submitting));

            var statusLines = View.Query<Label>("bug-report-status").ToList();
            Assert.AreEqual(1, statusLines.Count);
            Assert.AreEqual(expected, statusLines[0].PlainText());
        }
    }
}
