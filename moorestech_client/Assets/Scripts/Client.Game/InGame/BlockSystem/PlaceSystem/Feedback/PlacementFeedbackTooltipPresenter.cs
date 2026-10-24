using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     PlacementFeedback をカーソルツールチップへ反映する。行が無ければ自分が出した分だけ消す（DeleteObjectServiceと同じ規則）
    ///     Pushes PlacementFeedback into the cursor tooltip; with no lines it hides only what it showed itself (same rule as DeleteObjectService)
    /// </summary>
    public class PlacementFeedbackTooltipPresenter : IPlacementFeedbackPresenter
    {
        // 所有者トークン・同一内容の Show 省略・自分の分だけの Hide は共通の書き手が持つ
        // The shared writer holds the owner token, the unchanged-content Show skip and the own-only Hide
        private readonly OwnedTooltipLinesWriter _writer;

        public PlacementFeedbackTooltipPresenter(IMouseCursorTooltip tooltip)
        {
            _writer = new OwnedTooltipLinesWriter(tooltip);
        }

        public void Present(PlacementFeedback feedback)
        {
            if (feedback.Lines.Count == 0)
            {
                Hide();
                return;
            }

            // 他の書き手が出していても譲らず出す（自分の表示が立ったままで内容も同じなら Show を省く）
            // Show even while another writer is showing, without yielding (Show is skipped while ours stands with the same content)
            _writer.Show(feedback.Lines);
        }

        public void Hide()
        {
            _writer.Hide();
        }
    }
}
