using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Core.Item.Interface;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback
{
    /// <summary>
    /// 電線ドメイン専用のツールチップ行を組み立てる
    /// Builds the tooltip lines that belong to the electric-wire domain
    /// </summary>
    public static class ElectricWireFeedbackLines
    {
        // 接続距離から不足素材を算出して行にする（接続判定と同じConnectToolCostCalculatorを通す）
        // Derives the shortages from the connection distance and turns them into lines (through the same ConnectToolCostCalculator the judgement uses)
        public static IReadOnlyList<TooltipLine> WireShortageLines(Guid connectToolGuid, float distance, IEnumerable<IItemStack> inventoryItems)
        {
            return WireShortageLines(ConnectToolMaterialShortageCalculator.Calculate(connectToolGuid, distance, inventoryItems, null));
        }

        // 電線不足行は実アイテム名＋所持/必要で出す。算出不能時の落とし先キーはここが唯一の所有者
        // The wire shortage lines carry real item names with held/required; this is the single owner of the fallback key when nothing can be computed
        public static IReadOnlyList<TooltipLine> WireShortageLines(IReadOnlyList<ConstructionMaterialShortage> shortages)
        {
            return ConstructionMaterialShortageLine.ToLines(shortages, LocalizationKeys.Ui.Tooltip.PlaceWireFailed);
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
