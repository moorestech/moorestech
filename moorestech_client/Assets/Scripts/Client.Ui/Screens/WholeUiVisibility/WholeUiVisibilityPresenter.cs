using Client.Ui.Foundation.Root;
using Client.Ui.State.WholeUiVisibility;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.WholeUiVisibility
{
    /// <summary>
    /// Ctrl+U とカットシーンを合わせた値で GameUi 全体を出し入れする（全部隠し・HideAll）。隠し方はルートの持ち主 GameUiRoot が持つ（Web App.tsx:86。D-28）
    /// Shows or hides the whole GameUi (HideAll) from the combined Ctrl+U and cutscene value; the root owner GameUiRoot owns how to hide (Web App.tsx:86; D-28)
    /// </summary>
    public sealed class WholeUiVisibilityPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly WholeUiVisibilityModel _model;

        public WholeUiVisibilityPresenter(GameUiRoot gameUiRoot, WholeUiVisibilityModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            _model.IsShown.Subscribe(_gameUiRoot.SetWholeUiVisible);
        }
    }
}
