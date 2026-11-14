using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Shell
{
    /// <summary>
    /// 幕とヴィネットを起動時に最奥の層へ 1 回だけ載せ、画面ルータの判断で幕を出し入れする
    /// Mounts the dim and vignette once at startup in the back-most layer and toggles the dim on the screen router's decision
    /// </summary>
    public sealed class ScreenBackdropPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly UiRoutingState _routingState;
        private ScreenBackdropView _view;

        public ScreenBackdropPresenter(GameUiRoot gameUiRoot, UiRoutingState routingState)
        {
            _gameUiRoot = gameUiRoot;
            _routingState = routingState;
        }

        public void Initialize()
        {
            // 実画面の族の Backdrop 層（Web の z viewport-backdrop・.viewport の背景）
            // The viewport family of the Backdrop layer (Web z viewport-backdrop and the .viewport background)
            _view = new ScreenBackdropView();
            _gameUiRoot.GetLayer(UiLayer.Backdrop, UiFamily.Viewport).Add(_view);

            // ルータの値ごとに幕を出し入れする（前例 PauseMenuScreenPresenter と同じ形。結線は録画 uitk-hud が見る）
            // Toggle the dim on each router value (same shape as PauseMenuScreenPresenter; the recording uitk-hud checks the wiring)
            _routingState.Current.Subscribe(snapshot => _view.SetBackdropShown(ShowsBackdropOn(snapshot)));

            #region Internal

            // traits の Backdrop 列を読むのはここだけ（Web の screenShowsBackdrop。設計書 §4.4・A-44）
            // The only reader of the traits Backdrop column (Web screenShowsBackdrop; design §4.4, A-44)
            bool ShowsBackdropOn(UiRoutingSnapshot snapshot)
            {
                return UiScreenTraitsTable.Get(snapshot.Screen).Backdrop;
            }

            #endregion
        }

    }
}
