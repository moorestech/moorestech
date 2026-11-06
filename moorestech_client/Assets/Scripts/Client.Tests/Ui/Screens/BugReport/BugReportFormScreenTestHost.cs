using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// バグ報告の L1 の下ごしらえ。確保済み・全件送信済みのフォームを載せ、本文の入力と要素の引き方を揃える
    /// Setup for bug-report L1 tests: mounts a form with the capture ready and everything delivered, and shares typing and element lookups
    /// </summary>
    public abstract class BugReportFormScreenTestHost : ScreenTestHost
    {
        protected RecordingPauseMenuCommands Commands { get; private set; }
        protected BugReportDraft Draft { get; private set; }
        protected BugReportFormView View { get; private set; }

        [SetUp]
        public void MountForm()
        {
            // 基底の SetUp（言語を英語に固定・トーストの収集）の後に載せる
            // Mount after the base SetUp (English pinned, toasts collected)
            Commands = new RecordingPauseMenuCommands();
            Draft = new BugReportDraft();
            View = Mount(new BugReportFormView(Model(BugReportCaptureStatus.Ready), Commands, Toasts, Toaster, Draft));
        }

        protected static PauseMenuViewModel Model(string captureKind, params string[] missing)
        {
            return new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(captureKind, missing), PlaytestDeliveryAllSent.Instance, PlaytestExportIdle.Instance);
        }

        protected void Type(string text)
        {
            Description().value = text;
        }

        protected TextField Description() => Find<TextField>(View, "bug-report-description");
        protected FormButton SendButton() => Find<FormButton>(View, "bug-report-send");
        protected Label Status() => View.Q<Label>("bug-report-status");
    }
}
