using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     PlacementFeedback をカーソルツールチップへ反映する。行が無ければ自分が出した分だけ消す（DeleteObjectServiceと同じ規則）
    ///     Pushes PlacementFeedback into the cursor tooltip; with no lines it hides only what it showed itself (same rule as DeleteObjectService)
    /// </summary>
    public class PlacementFeedbackTooltipPresenter
    {
        private bool _isShown;

        public void Present(PlacementFeedback feedback)
        {
            if (feedback.Lines.Count == 0)
            {
                Hide();
                return;
            }

            MouseCursorTooltip.Instance.Show(feedback.Lines);
            _isShown = true;
        }

        public void Hide()
        {
            if (!_isShown) return;
            MouseCursorTooltip.Instance.Hide();
            _isShown = false;
        }
    }
}
