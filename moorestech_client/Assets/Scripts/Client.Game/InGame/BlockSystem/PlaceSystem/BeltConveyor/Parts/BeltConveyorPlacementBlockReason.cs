using System;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ベルト固有のセル設置不可理由を保持
    /// 経路計算が判定、systemが行を積む
    /// Holds the belt-specific reason a path cell cannot be placed
    /// Judged by the path calculation, turned into a line by the system
    /// </summary>
    public enum BeltConveyorPlacementBlockReason
    {
        None,
        ImpossibleOverpass,
        SlopeBlockMissing,

        // 張替え: 手持ちファミリーに既設と同じロール（分岐器等）が無い
        // Replace: the held family lacks the existing block's role (splitter etc.)
        ReplaceRoleMissing,
    }

    /// <summary>
    /// ベルト固有の不可理由をカーソルツールチップの辞書キーへ写像する
    /// Maps a belt-specific placement block reason to a cursor-tooltip dictionary key
    /// </summary>
    public static class BeltConveyorPlacementBlockReasonTooltipKey
    {
        public static LocalizationKey ToKey(BeltConveyorPlacementBlockReason reason)
        {
            return reason switch
            {
                BeltConveyorPlacementBlockReason.ImpossibleOverpass => LocalizationKeys.Ui.Tooltip.PlaceBeltOverpassInfeasible,
                BeltConveyorPlacementBlockReason.SlopeBlockMissing => LocalizationKeys.Ui.Tooltip.PlaceBeltNoSlopeBlock,
                BeltConveyorPlacementBlockReason.ReplaceRoleMissing => LocalizationKeys.Ui.Tooltip.PlaceBeltReplaceRoleMissing,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
