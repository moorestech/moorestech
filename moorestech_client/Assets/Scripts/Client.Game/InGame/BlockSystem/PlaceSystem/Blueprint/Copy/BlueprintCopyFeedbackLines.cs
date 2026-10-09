using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     範囲内のコピー対象数と空範囲の理由をツールチップへ積む
    ///     Adds the copy-target count and empty-range reason to the tooltip
    /// </summary>
    public static class BlueprintCopyFeedbackLines
    {
        public static void ReportBlocksInRange(int count, PlacementFeedback feedback)
        {
            feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.BlueprintCopyBlocksInRange, new[] { count.ToString() }));
            if (count == 0) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.BlueprintCopyEmptyRange));
        }
    }
}
