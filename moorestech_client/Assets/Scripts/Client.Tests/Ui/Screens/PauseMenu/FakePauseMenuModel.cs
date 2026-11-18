using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.PauseMenu;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズ画面の偽モデル。テストが Set で ViewModel を差し込む
    /// Fake pause-menu model; tests push view models with Set
    /// </summary>
    public sealed class FakePauseMenuModel : FakeModel<PauseMenuViewModel>
    {
        public FakePauseMenuModel(PauseMenuViewModel initial) : base(initial)
        {
        }

        // 切断なし・送信可・全件送信済み・書き出し無しの、指定ページの ViewModel
        // View model for the given page with no disconnect, a ready capture, everything sent and no export
        public static PauseMenuViewModel Page(PauseMenuPage page)
        {
            return new PauseMenuViewModel(page, false, Capture(BugReportCaptureStatus.Ready), PlaytestDeliveryAllSent.Instance, PlaytestExportIdle.Instance);
        }

        public static BugReportCaptureStatus Capture(string kind, params string[] missing)
        {
            return new BugReportCaptureStatus(kind, missing);
        }
    }
}
