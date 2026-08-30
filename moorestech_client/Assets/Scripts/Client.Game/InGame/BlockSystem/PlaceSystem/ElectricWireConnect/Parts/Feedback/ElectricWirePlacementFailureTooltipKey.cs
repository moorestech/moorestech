using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback
{
    /// <summary>
    /// 電線失敗理由をツールチップキーへ写像
    /// Maps a wire failure reason to a tooltip key
    /// </summary>
    public static class ElectricWirePlacementFailureTooltipKey
    {
        public static LocalizationKey ToKey(ElectricWirePlacementFailureReason reason)
        {
            return reason switch
            {
                ElectricWirePlacementFailureReason.OutOfRange => LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRange,
                ElectricWirePlacementFailureReason.AlreadyConnected => LocalizationKeys.Ui.Tooltip.PlaceWireAlreadyConnected,
                ElectricWirePlacementFailureReason.ConnectionLimit => LocalizationKeys.Ui.Tooltip.PlaceWireConnectionLimit,
                ElectricWirePlacementFailureReason.InvalidTarget => LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget,
                ElectricWirePlacementFailureReason.PositionOccupied => LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock,
                // 素材不足(NoWireItem)は名指しの行を素材ごとに積むためここでは写像しない
                // Material shortage (NoWireItem) is not mapped here; it becomes one named line per material
                // 上記以外（切断系・未解放・サーバー側のみの理由）はクライアントの設置判定では発生しないため既定文言へ
                // Everything else (disconnect-side, not-unlocked, server-only reasons) never arises in client placement judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceWireFailed,
            };
        }

        // 判定の失敗理由と消費電線数を積む（接続・延長設置の両モードで共有する手順）
        // Push the judgement failure reason and the wire cost (the shape both connect and extend modes shared)
        // 不足素材は判定と同じ入力から算出済みのものを受け取る（表示層が再算出すると予約分を取りこぼす）
        // The shortages arrive already derived from the judgement's own inputs; recomputing here would drop the reservation
        public static void Report(ElectricWireExtendPreviewData preview, PlacementFeedback feedback)
        {
            // 素材不足は複数行になりうる
            // Material shortage alone can span multiple lines
            if (!preview.IsPlaceable)
            {
                if (preview.Judgement.FailureReason == ElectricWirePlacementFailureReason.NoWireItem) feedback.AddLines(ElectricWireFeedbackLines.WireShortageLines(preview.MaterialShortages));
                else feedback.Add(new TooltipLine(ToKey(preview.Judgement.FailureReason)));
            }

            if (ElectricWireFeedbackLines.TryWireCost(preview.WireCostCount, out var costLine)) feedback.Add(costLine);
        }
    }
}
