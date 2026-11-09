using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     経路設置の理由をカーソルツールチップの辞書キーへ写す
    ///     Maps route-placement reasons to cursor-tooltip dictionary keys
    /// </summary>
    public static class BeltRouteTooltipKey
    {
        public static TooltipLine NoPathLine()
        {
            return new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteNotFound);
        }

        // 共有原因は共有の文言、ベルト固有の原因だけ専用の文言
        // Shared causes use the shared wording; only belt-specific causes get their own lines
        public static TooltipLine CellLine(BeltRouteCellBlockReason reason)
        {
            return reason switch
            {
                BeltRouteCellBlockReason.ExistingBlock => PlacementFeedback.BlockedByExistingBlockLine(),
                BeltRouteCellBlockReason.Terrain => PlacementFeedback.BlockedByTerrainLine(),
                BeltRouteCellBlockReason.SlopeHead => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteSlopeHead),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }

        public static TooltipLine BranchLine(BeltRouteBranchBlockReason reason)
        {
            return reason switch
            {
                BeltRouteBranchBlockReason.NotStraightBelt => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchNotStraightBelt),
                BeltRouteBranchBlockReason.NoSplitter => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchNoSplitter),
                BeltRouteBranchBlockReason.MultipleInputs => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchMultipleInputs),
                BeltRouteBranchBlockReason.UnusedExitHasReceiver => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchUnusedExitHasReceiver),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
