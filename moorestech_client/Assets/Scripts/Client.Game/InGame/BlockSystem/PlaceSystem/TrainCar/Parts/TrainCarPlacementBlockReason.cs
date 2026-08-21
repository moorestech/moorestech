using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    /// <summary>
    /// 列車配置候補が立たない理由。検出器が判定し、設置システムがツールチップ行にする
    /// Why no train placement candidate holds; judged by the detector and turned into a tooltip line by the place system
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
            return reason == TrainCarPlacementBlockReason.OverlapsExistingTrainUnit
                ? LocalizationKeys.Ui.Tooltip.PlaceTrainCarOverlapsTrain
                : LocalizationKeys.Ui.Tooltip.PlaceTrainCarNoRoute;
        }
    }
}
