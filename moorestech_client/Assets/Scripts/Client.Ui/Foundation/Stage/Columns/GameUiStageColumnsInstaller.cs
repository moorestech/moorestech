using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Foundation.Stage.Columns
{
    /// <summary>
    /// 上段 3 列を舞台の Screen 層へ 1 つ載せ、画面の性質の表に従って左の余白を畳む
    /// Mounts the single top-row column layout on the stage Screen layer and collapses its left padding per the screen-trait table
    /// </summary>
    public sealed class GameUiStageColumnsInstaller : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly StageColumnsLayout _layout;
        private readonly UiRoutingState _routing;

        public GameUiStageColumnsInstaller(GameUiRoot gameUiRoot, StageColumnsLayout layout, UiRoutingState routing)
        {
            _gameUiRoot = gameUiRoot;
            _layout = layout;
            _routing = routing;
        }

        public void Initialize()
        {
            // 画面ごとの列は各画面が Add する。ここは入れ物と余白だけ
            // Each screen adds its panel to a column; this only owns the container and padding
            _gameUiRoot.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(_layout);
            _routing.Current.Subscribe(snapshot => _layout.SetLeftPaddingCollapsed(UiScreenTraitsTable.Get(snapshot.Screen).CollapsesStageLeftPadding));
        }
    }
}
