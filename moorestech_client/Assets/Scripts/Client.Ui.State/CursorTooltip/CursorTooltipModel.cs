using Client.Game.InGame.UI.Tooltip;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの表示状態を購読し、画面向けの行の列にして配る
    /// Subscribes to the cursor tooltip presentation and publishes it as lines for the screen
    /// </summary>
    public sealed class CursorTooltipModel : IInitializable
    {
        private readonly MouseCursorTooltipState _tooltipState;
        private readonly ReactiveProperty<CursorTooltipViewModel> _current = new(CursorTooltipViewModel.Hidden);

        public IReadOnlyReactiveProperty<CursorTooltipViewModel> Current => _current;

        public CursorTooltipModel(MouseCursorTooltipState tooltipState)
        {
            _tooltipState = tooltipState;
        }

        public void Initialize()
        {
            // 源は ReactiveProperty なので購読した時点の表示も流れてくる
            // The source is a ReactiveProperty, so subscribing also delivers the current presentation
            _tooltipState.OnPresentationChanged.Subscribe(presentation => _current.Value = new CursorTooltipViewModel(presentation.Lines));
        }
    }
}
