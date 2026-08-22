using System.Linq;
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     PlacementFeedback をカーソルツールチップへ反映する。行が無ければ自分が出した分だけ消す（DeleteObjectServiceと同じ規則）
    ///     Pushes PlacementFeedback into the cursor tooltip; with no lines it hides only what it showed itself (same rule as DeleteObjectService)
    /// </summary>
    public class PlacementFeedbackTooltipPresenter
    {
        private readonly TooltipOwner _tooltipOwner = new();

        public void Present(PlacementFeedback feedback)
        {
            if (feedback.Lines.Count == 0)
            {
                Hide();
                return;
            }

            // TooltipPresentationは不変スナップショット前提のため、使い回しバッファではなく複製を渡す
            // TooltipPresentation assumes an immutable snapshot, so hand it a copy instead of the reused buffer
            MouseCursorTooltip.Instance.Show(_tooltipOwner, feedback.Lines.ToArray());
        }

        public void Hide()
        {
            MouseCursorTooltip.Instance.Hide(_tooltipOwner);
        }
    }
}
