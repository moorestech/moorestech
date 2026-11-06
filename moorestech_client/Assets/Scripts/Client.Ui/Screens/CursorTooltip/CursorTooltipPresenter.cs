using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.CursorTooltip;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップを Tooltip 層の Viewport 族へ起動時に 1 回だけ載せ、Model を購読して描かせる（Web の Portal・z 250・舞台の倍率で拡大）
    /// Mounts the cursor tooltip once at startup into the Tooltip layer's Viewport family and renders it from the model (Web Portal, z 250, scaled by the stage scale)
    /// </summary>
    public sealed class CursorTooltipPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly CursorTooltipModel _model;
        private readonly ICursorTooltipScreen _screen;

        public CursorTooltipPresenter(GameUiRoot gameUiRoot, CursorTooltipModel model, ICursorTooltipScreen screen)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _screen = screen;
        }

        public void Initialize()
        {
            // 載せてから購読する。購読時の今の値で 1 回描く（前例 ToastLayerPresenter）
            // Mount first, then subscribe; subscribing draws once with the current value (precedent ToastLayerPresenter)
            var view = new CursorTooltipView(_screen);
            _gameUiRoot.GetLayer(UiLayer.Tooltip, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}
