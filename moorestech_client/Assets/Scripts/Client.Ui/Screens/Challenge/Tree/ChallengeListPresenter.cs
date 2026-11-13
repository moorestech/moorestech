using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Challenge;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の登録・表示・描画（前例 ResearchTreePresenter）
    /// Mounts, shows and draws the challenge list (precedent ResearchTreePresenter)
    /// </summary>
    public sealed class ChallengeListPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly ChallengeListModel _model;
        private readonly UiRoutingState _routing;
        private readonly IChallengeListCommands _commands;
        private readonly IUiOsCursor _osCursor;

        public ChallengeListPresenter(GameUiRoot gameUiRoot, ChallengeListModel model, UiRoutingState routing, IChallengeListCommands commands, IUiOsCursor osCursor)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _routing = routing;
            _commands = commands;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            // 描いてから載せる（キーの無いラベルを載せない）。一覧は Web の --z-stage-overlay-panel
            // Render before mounting (no keyless labels attached); the list is the Web --z-stage-overlay-panel
            var view = new ChallengeListView(_commands, _osCursor);
            view.Render(_model.Current.Value);
            _gameUiRoot.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(view);
            _routing.Current.Subscribe(snapshot => view.SetShown(ChallengeScreenVisibility.IsShown(snapshot)));
            _model.Current.Subscribe(view.Render);
        }
    }
}
