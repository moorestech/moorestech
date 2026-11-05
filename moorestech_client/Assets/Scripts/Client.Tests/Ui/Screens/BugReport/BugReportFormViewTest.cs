using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// バグ報告の送信可否・状態行・送信の流れ・下書き・種別の文言・救出操作を確かめる（L1。Web の BugReportForm の vitest と feedbackReportLabels.spec.ts）
    /// Verifies send availability, status line, send flow, draft, kind labels and rescue controls (L1; Web BugReportForm vitest and feedbackReportLabels.spec.ts)
    /// </summary>
    public class BugReportFormViewTest : ScreenTestHost
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

        [Test]
        public void 確保中は送信ボタンを押せず状態行は確保中の1本だけ()
        {
            Type("broken belt");
            _view.Apply(Model(BugReportCaptureStatus.Capturing, "video"));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.AreEqual(1, _view.Query<Label>("bug-report-status").ToList().Count);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BugReport.CapturePending).Resolve(), Status().text);
        }

        [Test]
        public void 確保セッションが無いときは押せず理由を出す()
        {
            Type("broken belt");
            _view.Apply(Model(BugReportCaptureStatus.NoSession));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.IsNotNull(Status());
        }

        [Test]
        public void 送れる状態なら欠損の一覧を同じ1本の行で出す()
        {
            Assert.IsNull(_view.Q<Label>("bug-report-status"));
            _view.Apply(Model(BugReportCaptureStatus.Ready, "video", "logs"));
            StringAssert.Contains("video, logs", Status().text);
        }

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

        [Test]
        public void 送信が拒否されると成功通知を出さず下書きを残す()
        {
            simulate.Click(Find<Button>(_view, "bug-report-kind-feedback"));
            Type("nice");
            simulate.Click(SendButton());
            _commands.CompleteSubmit(new UiCommandRejected(UiCommandRejectReason.CaptureRefused, ""));

            Assert.AreEqual("bug_report.submit failed: capture_refused", ShownToasts.Single().Text);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
            Assert.AreEqual("nice", _draft.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, _draft.Kind);
            Assert.IsTrue(SendButton().enabledSelf);
        }

        [Test]
        public void 応答待ちが終わってもホスト送信中は種別と本文を保護する()
        {
            simulate.Click(Find<Button>(_view, "bug-report-kind-feedback"));
            Type("nice");
            _view.Apply(Model(BugReportCaptureStatus.Submitting));
            Assert.IsFalse(Description().enabledSelf);
            Assert.IsFalse(Find<Button>(_view, "bug-report-kind-bug").enabledSelf);
            Assert.IsFalse(SendButton().enabledSelf);
            StringAssert.Contains("impressions", Status().text);
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
