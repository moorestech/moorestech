using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 記述欄・送信可否・状態行・送信中ロック（L1）
    /// Description, send availability, status line, submit lock (L1)
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
            _view = Mount(new BugReportFormView(Model(BugReportCaptureStatus.Ready), _commands, Toasts, _draft));
        }

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
            _view.Apply(Model(BugReportCaptureStatus.Capturing, "video"));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.AreEqual(1, _view.Query<Label>("bug-report-status").ToList().Count);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BugReport.CapturePending).Resolve(), Status().text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">確保セッションが無いときは押せず理由を出す</web-test>
        [Test]
        public void 確保セッションが無いときは押せず理由を出す()
        {
            Type("broken belt");
            _view.Apply(Model(BugReportCaptureStatus.NoSession));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.IsNotNull(Status());
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">送れる状態なら欠損の一覧を同じ1本の行で出す</web-test>
        [Test]
        public void 送れる状態なら欠損の一覧を同じ1本の行で出す()
        {
            Assert.IsNull(_view.Q<Label>("bug-report-status"));
            _view.Apply(Model(BugReportCaptureStatus.Ready, "video", "logs"));
            StringAssert.Contains("video, logs", Status().text);
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
