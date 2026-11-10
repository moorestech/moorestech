using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Research
{
    /// <summary>
    /// 研究エリアの登録・表示・描画
    /// Mounts, shows and draws the research area
    /// </summary>
    public sealed class ResearchTreePresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly ResearchTreeModel _model;
        private readonly UiRoutingState _routing;
        private readonly IResearchTreeCommands _commands;
        private readonly GameContentPresentation _content;
        private readonly UiCommandResultToaster _toaster;
        private readonly IUiOsCursor _osCursor;

        public ResearchTreePresenter(GameUiRoot gameUiRoot, ResearchTreeModel model, UiRoutingState routing, IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _routing = routing;
            _commands = commands;
            _content = content;
            _toaster = toaster;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            // 描いてから載せる（キーの無いラベルを載せない）。研究エリアは Web の --z-stage-overlay-panel
            // Render before mounting (no keyless labels attached); the research area is the Web --z-stage-overlay-panel
            var view = new ResearchTreeView(_commands, _content, _toaster, _osCursor);
            view.Render(_model.Current.Value);
            _gameUiRoot.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(view);
            _routing.Current.Subscribe(snapshot => view.SetShown(snapshot.Screen == UiScreen.ResearchTree));
            _model.Current.Subscribe(view.Render);
        }
    }
}
