using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback
{
    /// <summary>
    /// 電線ドメイン専用のツールチップ行を組み立てる
    /// Builds the tooltip lines that belong to the electric-wire domain
    /// </summary>
    public static class ElectricWireFeedbackLines
    {
        // 電線不足行は実アイテム名＋所持/必要で出す。算出不能時の落とし先キーはここが唯一の所有者
        // The wire shortage lines carry real item names with held/required; this is the single owner of the fallback key when nothing can be computed
        // 不足素材は判定側（自動接続プレビュー・延長プレビュー）が算出したものだけを受け取り、ここでは算出しない
        // The shortages only ever arrive from the judgement side (auto-connect and extend previews) so the two can never spell them differently
        // 行の生成と同一アイテムの畳み込みはPlacementFeedback側の関門が担う
        // Building the lines and folding duplicates of the same item is the PlacementFeedback gate's job
        public static void ReportWireShortages(IReadOnlyList<ConstructionMaterialShortage> shortages, PlacementFeedback feedback)
        {
            feedback.AddMaterialShortagesOrFallback(shortages, LocalizationKeys.Ui.Tooltip.PlaceWireFailed);
        }

        public static TooltipLine WireOutOfRangeNotice()
        {
            return new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice);
        }

        // 消費電線が無いときは案内行を出さない（旧ラベルと同じ）
        // No notice line without wire consumption (same as the old label)
        public static bool TryWireCost(int totalWireCost, out TooltipLine line)
        {
            if (totalWireCost <= 0)
            {
                line = default;
                return false;
            }

            line = new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { totalWireCost.ToString() });
            return true;
        }
    }
}
