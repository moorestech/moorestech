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
                // 素材不足(NotEnoughRailItem)は名指しの行を素材ごとに積むためここでは写像しない
                // Material shortage (NotEnoughRailItem) is not mapped here; it becomes one named line per material
                // 上記以外（未解放・サーバー側のみの理由）はクライアントの接続判定では発生しないため既定文言へ
                // Everything else (not-unlocked, server-only reasons) never arises in client connection judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceRailFailed,
            };
        }

        // 判定の失敗理由とカーブ半径不足を個別行でツールチップへ積む
        // Push the judgement failure reason and the too-tight curve as separate tooltip lines
        public static void Report(TrainRailConnectPreviewData previewData, PlacementFeedback feedback)
        {
            // レールは複数素材消費で複数行になる
            // A rail's multi-material cost becomes multiple lines
            // 橋脚の建設コストと同じアイテムが並ばないよう、行生成と畳み込みは関門へ委ねる
            // Building and folding the lines is delegated to the gate so the pier's construction cost never doubles the same item
            if (previewData.FailureReason == RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem) feedback.AddMaterialShortagesOrFallback(previewData.MaterialShortages, LocalizationKeys.Ui.Tooltip.PlaceRailFailed);
            else if (previewData.FailureReason != RailConnectionEditProtocol.RailConnectionEditFailureReason.None) feedback.Add(new TooltipLine(ToKey(previewData.FailureReason)));

            if (!previewData.IsCurvePlaceable) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight));
        }
    }
}
