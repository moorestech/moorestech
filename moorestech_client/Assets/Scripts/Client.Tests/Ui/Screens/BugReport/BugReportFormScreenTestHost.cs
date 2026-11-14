using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// バグ報告 L1 の下準備。送信済みフォームを載せ本文入力を揃える
    /// Setup for bug-report L1: mounts a delivered form and shares typing
    /// </summary>
    public abstract class BugReportFormScreenTestHost : ScreenTestHost
    {
        protected RecordingPauseMenuCommands Commands { get; private set; }
        protected BugReportDraft Draft { get; private set; }
        protected BugReportFormView View { get; private set; }

        [SetUp]
        public void MountForm()
        {
            // 基底 SetUp の後に載せる
            // Mount after the base SetUp
            Commands = new RecordingPauseMenuCommands();
            Draft = new BugReportDraft();
            View = Mount(new BugReportFormView(Model(BugReportCaptureStatus.Ready), Commands, Toasts, Toaster, Draft, new UiOsCursorRequestStack(new FakeUiOsCursorApplier())));
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
