using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール失敗理由をツールチップキーへ写像
    /// Maps a rail failure reason to a tooltip key
    /// </summary>
    public static class TrainRailPlacementFailureTooltipKey
    {
        public static LocalizationKey ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason reason)
        {
            return reason switch
            {
                RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded => LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded,
                RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem => LocalizationKeys.Ui.Tooltip.PlaceRailNotEnoughRailItem,
                // 上記以外（未解放・サーバー側のみの理由）はクライアントの接続判定では発生しないため既定文言へ
                // Everything else (not-unlocked, server-only reasons) never arises in client connection judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceRailFailed,
            };
        }

        // 判定の失敗理由とカーブ半径不足を個別行でツールチップへ積む
        // Push the judgement failure reason and the too-tight curve as separate tooltip lines
        public static void Report(TrainRailConnectPreviewData previewData, PlacementFeedback feedback)
        {
            if (previewData.FailureReason != RailConnectionEditProtocol.RailConnectionEditFailureReason.None) feedback.Add(new TooltipLine(ToKey(previewData.FailureReason)));
            if (!previewData.IsCurvePlaceable) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight));
        }
    }
}
