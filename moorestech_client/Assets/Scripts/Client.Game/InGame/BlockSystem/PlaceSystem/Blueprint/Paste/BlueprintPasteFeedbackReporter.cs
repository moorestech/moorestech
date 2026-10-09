using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     BPの共有判定を設置理由行へ変換する
    ///     Converts shared blueprint judgements into placement reason lines
    /// </summary>
    public static class BlueprintPasteFeedbackReporter
    {
        public static void Report(BlueprintPastePlan plan, PlacementFeedback feedback)
        {
            // 確定済みの共有判定からだけ理由行を作る
            // Build reason lines solely from the completed shared judgement
            if (0 < plan.Copies.Count && plan.CountCopies(BlueprintPasteCopyState.AllOverlapped) == plan.Copies.Count) feedback.AddBlockedByExistingBlock();
            if (0 < plan.CountCopies(BlueprintPasteCopyState.GroundNotFound)) feedback.AddGroundNotFound();
            if (0 < plan.CountCopies(BlueprintPasteCopyState.NotUnlocked)) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlueprintNotUnlocked));
            if (0 < plan.CountCopies(BlueprintPasteCopyState.NoResolvedBlocks)) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlueprintMissingBlocks));
            if (0 < plan.CountCopies(BlueprintPasteCopyState.InvalidCoordinates)) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Notification.BlueprintPasteInvalidRequest));
            ReportLines(plan, feedback);
            if (0 < plan.ShortageRequirements.Count) feedback.AddMaterialShortages(ConstructionCostShortageCalculator.ToShortages(plan.ShortageRequirements));
        }

        private static void ReportLines(BlueprintPastePlan plan, PlacementFeedback feedback)
        {
            var reported = new HashSet<LocalizationKey>();
            foreach (var copy in plan.Copies)
            foreach (var line in copy.Draft.Lines)
            {
                if (line.IsConnectable) continue;

                // 無効線だけの理由を種類ごとに一行へ畳む
                // Deduplicate invalid-line reasons by connection kind
                var key = line.Kind == BlueprintPasteLineKind.ElectricWire
                    ? WireReason(line.FailureReason) : ChainReason(line.FailureReason);
                if (reported.Add(key)) feedback.Add(new TooltipLine(key));
            }
        }

        private static LocalizationKey WireReason(BlueprintPasteLineFailureReason reason)
        {
            return reason switch
            {
                BlueprintPasteLineFailureReason.OutOfRange => LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRange,
                BlueprintPasteLineFailureReason.ConnectionLimit => LocalizationKeys.Ui.Tooltip.PlaceWireConnectionLimit,
                BlueprintPasteLineFailureReason.AlreadyConnected => LocalizationKeys.Ui.Tooltip.PlaceWireAlreadyConnected,
                _ => LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget,
            };
        }

        private static LocalizationKey ChainReason(BlueprintPasteLineFailureReason reason)
        {
            return reason switch
            {
                BlueprintPasteLineFailureReason.OutOfRange => LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar,
                BlueprintPasteLineFailureReason.ConnectionLimit => LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit,
                BlueprintPasteLineFailureReason.AlreadyConnected => LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected,
                _ => LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed,
            };
        }
    }
}
