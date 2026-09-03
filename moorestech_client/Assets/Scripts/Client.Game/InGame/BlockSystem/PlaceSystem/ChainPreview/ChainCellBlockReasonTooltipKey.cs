using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結セルの不可原因をツールチップキーへ写像
    ///     Maps a chain cell block reason to a tooltip key
    /// </summary>
    public static class ChainCellBlockReasonTooltipKey
    {
        public static LocalizationKey ToKey(ChainCellBlockReason reason)
        {
            return reason switch
            {
                ChainCellBlockReason.GroundNotFound => LocalizationKeys.Ui.Tooltip.PlaceChainGroundNotFound,
                ChainCellBlockReason.GroundHeightMismatch => LocalizationKeys.Ui.Tooltip.PlaceChainGroundHeightMismatch,
                // 重なりと、原因が付かないまま不可になった場合は占有の文言に寄せる
                // Overlap, and any reason that arrives without a cause, fall back to the occupied wording
                _ => LocalizationKeys.Ui.Tooltip.PlaceChainBlocked,
            };
        }
    }
}
