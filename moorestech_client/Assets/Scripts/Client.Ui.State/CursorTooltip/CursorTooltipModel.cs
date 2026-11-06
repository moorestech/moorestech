using Client.Game.InGame.UI.Tooltip;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// ツールチップ状態を行の列で配る
    /// Publishes the tooltip state as lines
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
            // 購読時の表示も流れる
            // Subscribing also delivers the current state
            _tooltipState.OnPresentationChanged.Subscribe(presentation => _current.Value = new CursorTooltipViewModel(presentation.Lines));
        }
    }
}
