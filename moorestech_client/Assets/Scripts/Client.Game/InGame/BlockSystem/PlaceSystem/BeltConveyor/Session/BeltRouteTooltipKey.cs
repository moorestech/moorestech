using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
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
        public static TooltipLine NoPathLine(BeltRouteNoPathReason reason)
        {
            return reason switch
            {
                BeltRouteNoPathReason.Terrain => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteBlockedByTerrain),
                BeltRouteNoPathReason.ExistingBlock => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteBlockedByExistingBlock),
                BeltRouteNoPathReason.Both => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteBlockedByBoth),
                BeltRouteNoPathReason.OutOfRange => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteOutOfRange),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
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
                BeltRouteCellBlockReason.OutOfRange => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteOutOfRange),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }

        public static TooltipLine BranchLine(BeltRouteBranchBlockReason reason)
        {
            return reason switch
            {
                BeltRouteBranchBlockReason.NotStraightBelt => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchNotStraightBelt),
                BeltRouteBranchBlockReason.NoSplitter => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchNoSplitter),
                BeltRouteBranchBlockReason.SideInputOccupied => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchSideInputOccupied),
                BeltRouteBranchBlockReason.OtherSideHasReceiver => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchOtherSideHasReceiver),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
