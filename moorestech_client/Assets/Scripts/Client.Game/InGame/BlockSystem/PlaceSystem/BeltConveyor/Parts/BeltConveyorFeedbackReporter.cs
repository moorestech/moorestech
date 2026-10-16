using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ベルト固有の不可理由と地形乗り越え断念の案内を積む
    /// Pushes belt-specific block reasons and the terrain-climb-abandoned notice
    /// </summary>
    public static class BeltConveyorFeedbackReporter
    {
        public static void Report(PlacementFeedback feedback, BeltConveyorPlacementBlockReason cursorCellReason, bool terrainClimbAbandoned)
        {
            if (cursorCellReason != BeltConveyorPlacementBlockReason.None)
            {
                feedback.Add(new TooltipLine(BeltConveyorPlacementBlockReasonTooltipKey.ToKey(cursorCellReason)));
            }

            // 断念は列全体の判断なのでカーソル位置によらず案内する
            // Abandonment applies to the run, so show its notice regardless of cursor position
            if (terrainClimbAbandoned) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltTerrainClimbAbandoned));
        }
    }
}
