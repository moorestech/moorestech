using System;
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
                ElectricWirePlacementFailureReason.NoWireItem => LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem,
                ElectricWirePlacementFailureReason.InvalidTarget => LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget,
                ElectricWirePlacementFailureReason.PositionOccupied => LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock,
                // 上記以外（切断系・未解放・サーバー側のみの理由）はクライアントの設置判定では発生しないため既定文言へ
                // Everything else (disconnect-side, not-unlocked, server-only reasons) never arises in client placement judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceWireFailed,
            };
        }

        // 判定の失敗理由と消費電線数を積む（接続・延長設置の両モードで共有する手順）
        // Push the judgement failure reason and the wire cost (the shape both connect and extend modes shared)
        public static void Report(ElectricWirePlacementJudgement judgement, Guid connectToolGuid, float distance, PlacementFeedback feedback)
        {
            if (!judgement.IsPlaceable) feedback.Add(new TooltipLine(ToKey(judgement.FailureReason)));
            if (ElectricWireFeedbackLines.TryWireCost(ResolveCostCount(judgement, connectToolGuid, distance), out var costLine)) feedback.Add(costLine);
        }

        // 成功/失敗どちらもコストを返す(失敗時は距離算出)
        // Returns a cost on success or failure (failure derives it from distance)
        private static int ResolveCostCount(ElectricWirePlacementJudgement judgement, Guid connectToolGuid, float distance)
        {
            if (judgement.IsPlaceable) return judgement.WireCost.TotalCount;
            return ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, distance, out var cost) ? cost.TotalCount : 0;
        }
    }
}
