using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.CursorTooltip;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ツールチップを起動時に 1 回載せ、Model を購読して描く
    /// Mounts the tooltip once at startup and renders from the model
    /// </summary>
    public sealed class CursorTooltipPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly CursorTooltipModel _model;
        private readonly ICursorTooltipScreen _screen;
        private readonly IUiScreenPointer _pointer;
        private readonly StageScaleSource _stageScale;

        public CursorTooltipPresenter(GameUiRoot gameUiRoot, CursorTooltipModel model, ICursorTooltipScreen screen, IUiScreenPointer pointer, StageScaleSource stageScale)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _screen = screen;
            _pointer = pointer;
            _stageScale = stageScale;
        }

        public void Initialize()
        {
            // 載せてから購読する。購読時の今の値で 1 回描く（前例 ToastLayerPresenter）
            // Mount first, then subscribe; subscribing draws once with the current value (precedent ToastLayerPresenter)
            var view = new CursorTooltipView(_screen, _pointer, _stageScale);
            _gameUiRoot.GetLayer(UiLayer.Tooltip, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}
