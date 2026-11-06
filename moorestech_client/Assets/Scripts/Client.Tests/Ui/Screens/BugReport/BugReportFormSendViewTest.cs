using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Ui.Screens.BugReport;
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
    public class BugReportFormSendViewTest : ScreenTestHost
    {
        private RecordingPauseMenuCommands _commands;
        private BugReportDraft _draft;
        private BugReportFormView _view;

        [SetUp]
        public void MountForm()
        {
            _commands = new RecordingPauseMenuCommands();
            _draft = new BugReportDraft();
            _view = Mount(new BugReportFormView(Model(BugReportCaptureStatus.Ready), _commands, Toasts, _draft, Catalog));
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">入力後の送信で bug_report.submit を説明文付きで送りトーストを出す</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">送信後に画面遷移のactionは出さない</web-test>
        [Test]
        public void 入力後の送信で説明文付きで送り成功のトーストを出し画面遷移は出さない()
        {
            Type("  broken belt  ");
            simulate.Click(SendButton());
            _commands.CompleteSubmit(UiCommandAccepted.Instance);

            var submit = _commands.Issued.Single();
            Assert.AreEqual(PauseMenuCommandKind.SubmitBugReport, submit.Kind);
            Assert.AreEqual("broken belt", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Bug, submit.ReportKind);
            Assert.AreEqual(GameUiToastVariant.Info, ShownToasts.Single().Variant);
            Assert.AreEqual(new UiText(BugReportFormText.SentToast(PlaytestReportKind.Bug)).Resolve(), ShownToasts.Single().Text);
            Assert.AreEqual("", _draft.Description);
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
            Assert.IsFalse(Find<Button>(_view, "bug-report-kind-feedback").enabledSelf);
            simulate.Click(SendButton());
            Assert.AreEqual(1, _commands.Issued.Count);

            _commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.IsTrue(Description().enabledSelf);
        }

        [Test]
        public void 送信中に下書きが作り直されたら古い送信の完了は新しい下書きを消さない()
        {
            Type("report A");
            simulate.Click(SendButton());

            // 送信中にポーズを開き直して別の本文を書き始めた状態
            // The pause was reopened during the send and a different description was started
            _draft.Reset();
            _draft.SetDescription("report B");

            LogAssert.Expect(LogType.Warning, "[BugReportFormView] draft was reset during the send; keeping the newer draft");
            _commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual("report B", _draft.Description);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">確保済みの欠損を成功トーストに出す</web-test>
        [Test]
        public void 確保済みの欠損は送信成功時にエラーのトーストで出す()
        {
            _view.Apply(Model(BugReportCaptureStatus.Ready, "video"));
            Type("broken belt");
            simulate.FrameUpdate();
            simulate.Click(SendButton());

            // 送信中に欠損が消えても押した時点の欠損で伝える
            // Even if the gap clears during the send, report the gap seen at click time
            _view.Apply(Model(BugReportCaptureStatus.Ready));
            _commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
            StringAssert.Contains("video", ShownToasts.Single().Text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">送信成功後は種別が既定のバグへ戻る</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/submissionResults.test.ts">感想へ切り替えるとkindがfeedbackになる</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想のボタンと成功通知を選び、バグのボタンへ戻せる</web-test>
        [Test]
        public void 感想へ切り替えると種別と文言が感想になり送信成功後はバグへ戻る()
        {
            simulate.Click(Find<Button>(_view, "bug-report-kind-feedback"));
            Assert.AreEqual(PlaytestReportKind.Feedback, _draft.Kind);
            Assert.AreEqual("Send impressions", SendButton().Q<Label>().text);
            Type("nice");
            simulate.Click(SendButton());
            Assert.AreEqual(PlaytestReportKind.Feedback, _commands.Issued.Single().ReportKind);
            StringAssert.Contains("impressions", Status().text);

            _commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual(new UiText(BugReportFormText.SentToast(PlaytestReportKind.Feedback)).Resolve(), ShownToasts.Single().Text);
            Assert.AreEqual(PlaytestReportKind.Bug, _draft.Kind);
            Assert.AreEqual("Send bug report", SendButton().Q<Label>().text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想送信が失敗すると成功通知を出さずdraftを残す</web-test>
        [Test]
        public void 送信が拒否されると成功通知を出さず下書きを残す()
        {
            simulate.Click(Find<Button>(_view, "bug-report-kind-feedback"));
            Type("nice");
            simulate.Click(SendButton());
            _commands.CompleteSubmit(new UiCommandRejected(UiCommandRejectReason.CaptureRefused, ""));

            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.CommandRejected.CaptureRefused), ShownToasts.Single().Text, "トーストに未翻訳の失敗コードが出た");
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
            Assert.AreEqual("nice", _draft.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, _draft.Kind);
            Assert.IsTrue(SendButton().enabledSelf);
        }

        private static PauseMenuViewModel Model(string captureKind, params string[] missing)
        {
            return new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(captureKind, missing), PlaytestDeliveryAllSent.Instance, PlaytestExportIdle.Instance);
        }

        private void Type(string text)
        {
            Description().value = text;
        }

        private TextField Description() => Find<TextField>(_view, "bug-report-description");
        private Button SendButton() => Find<Button>(_view, "bug-report-send");
        private Label Status() => _view.Q<Label>("bug-report-status");
    }
}
