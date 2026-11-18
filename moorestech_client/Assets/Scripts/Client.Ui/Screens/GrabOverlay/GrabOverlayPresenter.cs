using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Inventory.Grab;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// 掴み絵を viewport の Grab 層へ 1 つ載せ、Model の表示を描かせる（前例 CursorTooltipPresenter）
    /// Mounts the single grab picture on the viewport Grab layer and renders the model's view (precedent CursorTooltipPresenter)
    /// </summary>
    public sealed class GrabOverlayPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly IUiScreenPointer _pointer;
        private readonly GrabOverlayModel _model;
        private readonly GameContentPresentation _content;

        public GrabOverlayPresenter(GameUiRoot gameUiRoot, IUiScreenPointer pointer, GrabOverlayModel model, GameContentPresentation content)
        {
            _gameUiRoot = gameUiRoot;
            _pointer = pointer;
            _model = model;
            _content = content;
        }

        public void Initialize()
        {
            var view = new GrabOverlayView(_pointer, _content);
            _gameUiRoot.GetLayer(UiLayer.Grab, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}
