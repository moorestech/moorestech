using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Routing;
using Client.Ui.State.Skit;
using Client.Ui.State.Toast;
using Client.Ui.State.WholeUiVisibility;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Skit
{
    /// <summary>
    /// スキットの画面と暗転を層へ 1 回だけ載せ、store の提示・入力可否・Enter を View へ押す。ゲームと同じ寿命なので購読は張りっぱなしで IDisposable を付けない
    /// Mounts the skit screen and blackout on their layers once and pushes the store presentation, input allowance and Enter into the views; game-lifetime, so the subscriptions stay and it carries no IDisposable
    /// </summary>
    public sealed class SkitScreenPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly UiRoutingState _routingState;
        private readonly SkitPresentationModel _model;
        private readonly WholeUiVisibilityModel _wholeUiVisibility;
        private readonly ISkitCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly IUiOsCursor _osCursor;

        public SkitScreenPresenter(GameUiRoot gameUiRoot, UiRoutingState routingState, SkitPresentationModel model, WholeUiVisibilityModel wholeUiVisibility, ISkitCommands commands, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            _gameUiRoot = gameUiRoot;
            _routingState = routingState;
            _model = model;
            _wholeUiVisibility = wholeUiVisibility;
            _commands = commands;
            _toaster = toaster;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            // 会話は SkitStage、暗転は SkitTransition の実画面族へ載せる（Web の viewportOverlay と Portal）。View は先に載せる（最初の Render を復元として全文で出すため）
            // The skit goes on SkitStage and the blackout on SkitTransition, both in the viewport family (Web viewportOverlay and portal); mount the view first so the first Render is a restore shown whole
            var view = new SkitScreenView(_commands, _toaster, _osCursor);
            _gameUiRoot.GetLayer(UiLayer.SkitStage, UiFamily.Viewport).Add(view);
            var transition = new SkitTransitionView();
            _gameUiRoot.GetLayer(UiLayer.SkitTransition, UiFamily.Viewport).Add(transition);

            // 提示・入力可否を購読し Enter をつなぐ
            // Subscribe to presentation and allowance; wire Enter
            _model.Current.Subscribe(model =>
            {
                view.Render(model);
                transition.SetShown(model.TransitionVisible);
            });
            _routingState.Current.Subscribe(snapshot => view.SetInteractive(AcceptsSkitInput(snapshot)));
            new SkitEnterKeyBinding(SkitEnterKeySource.ObservePressed(), view, _wholeUiVisibility.IsShown);

            #region Internal

            // 会話の入力を受ける画面の列挙は traits 表に任せる（Web の screenTraits の skitInput）
            // The traits table enumerates screens that accept skit input (Web screenTraits skitInput)
            bool AcceptsSkitInput(UiRoutingSnapshot snapshot)
            {
                return UiScreenTraitsTable.Get(snapshot.Screen).SkitInput;
            }

            #endregion
        }
    }
}
