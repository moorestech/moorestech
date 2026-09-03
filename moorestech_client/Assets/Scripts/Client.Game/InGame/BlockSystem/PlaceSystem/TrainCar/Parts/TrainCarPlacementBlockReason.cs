using System;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    /// <summary>
    /// 列車配置不可理由を保持
    /// Holds the reason a train placement candidate fails
    /// 検出器が判定、systemが行を積む
    /// Judged by the detector, turned into a line by the system
    /// </summary>
    public enum TrainCarPlacementBlockReason
    {
        None,
        NoRouteForTrainLength,
        OverlapsExistingTrainUnit,
    }

    /// <summary>
    /// 列車配置の不可理由をカーソルツールチップの辞書キーへ写像する
    /// Maps a train car placement block reason to a cursor-tooltip dictionary key
    /// </summary>
    public static class TrainCarPlacementBlockReasonTooltipKey
    {
        public static LocalizationKey ToKey(TrainCarPlacementBlockReason reason)
        {
            return reason switch
            {
                TrainCarPlacementBlockReason.OverlapsExistingTrainUnit => LocalizationKeys.Ui.Tooltip.PlaceTrainCarOverlapsTrain,
                TrainCarPlacementBlockReason.NoRouteForTrainLength => LocalizationKeys.Ui.Tooltip.PlaceTrainCarNoRoute,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
