using System;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// ポーズ画面の源 5 本を購読し、ViewModel 1 つへまとめて配る
    /// Subscribes to the pause menu's five sources and publishes them as one view model
    /// </summary>
    public sealed class PauseMenuModel : IInitializable
    {
        private readonly NetworkDisconnectState _disconnectState;
        private readonly BugReportCaptureSession _captureSession;
        private readonly PauseMenuStateService _pauseMenuStateService;
        private readonly ReactiveProperty<PauseMenuViewModel> _current;

        public IReadOnlyReactiveProperty<PauseMenuViewModel> Current => _current;
        public IObservable<Unit> OnPauseMenuOpened => _pauseMenuStateService.OnPauseMenuOpened;

        public PauseMenuModel(NetworkDisconnectState disconnectState, BugReportCaptureSession captureSession, PauseMenuStateService pauseMenuStateService)
        {
            _disconnectState = disconnectState;
            _captureSession = captureSession;
            _pauseMenuStateService = pauseMenuStateService;
            _current = new ReactiveProperty<PauseMenuViewModel>(Build());
        }

        public void Initialize()
        {
            // 源はどれも購読時に現在値を流すので、初回は捨てて変化だけで組み直す
            // Every source replays its current value on subscribe, so skip that and rebuild only on changes
            _disconnectState.OnDisconnectedChanged.Skip(1).Subscribe(_ => Rebuild());
            _captureSession.Status.Skip(1).Subscribe(_ => Rebuild());
            PlaytestDeliveryStatusBoard.Current.Skip(1).Subscribe(_ => Rebuild());
            PlaytestExportStatusBoard.Current.Skip(1).Subscribe(_ => Rebuild());
            _pauseMenuStateService.CurrentPage.Skip(1).Subscribe(_ => Rebuild());

            // ctor から購読までの間の変化を取り込む
            // Pick up any change made between construction and subscription
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = Build();
        }

        private PauseMenuViewModel Build()
        {
            return new PauseMenuViewModel(
                _pauseMenuStateService.CurrentPage.Value,
                _disconnectState.IsDisconnected,
                _captureSession.Status.Value,
                PlaytestDeliveryStatusBoard.Current.Value,
                PlaytestExportStatusBoard.Current.Value);
        }
    }
}
