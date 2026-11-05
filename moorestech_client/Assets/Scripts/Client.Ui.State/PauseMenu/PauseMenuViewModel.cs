using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// ポーズ画面の表示に要る値の不変スナップショット
    /// Immutable snapshot of the values the pause menu displays
    /// </summary>
    public sealed class PauseMenuViewModel
    {
        public PauseMenuPage Page { get; }
        public bool Disconnected { get; }
        public BugReportCaptureStatus Capture { get; }
        public PlaytestDeliveryStatus Delivery { get; }
        public PlaytestExportStatus Export { get; }

        public PauseMenuViewModel(PauseMenuPage page, bool disconnected, BugReportCaptureStatus capture, PlaytestDeliveryStatus delivery, PlaytestExportStatus export)
        {
            Page = page;
            Disconnected = disconnected;
            Capture = capture;
            Delivery = delivery;
            Export = export;
        }
    }
}
