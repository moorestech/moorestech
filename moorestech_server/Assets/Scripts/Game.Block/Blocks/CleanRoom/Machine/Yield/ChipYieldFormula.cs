using System;

namespace Game.Block.Blocks.CleanRoom.Machine.Yield
{
    /// <summary>
    ///     チップ歩留まりの確率式。抽選と機械UIの表示が CleanRoomRoleRates を通して同じ式を使う
    ///     Probability formulas for chip yield; the draws and the machine UI use them through CleanRoomRoleRates
    /// </summary>
    public static class ChipYieldFormula
    {
        // グレード上限＝装着品質モジュールの最高tier+1。品質モジュール無し（tier0）はG1だけ
        // Grade cap = highest equipped quality tier + 1; no quality module (tier 0) allows only G1
        public static int MaxWaferGrade(int highestQualityModuleTier, int gradeCount)
        {
            return Math.Clamp(highestQualityModuleTier + 1, 1, gradeCount);
        }

        // 1段の昇格確率 u = min(上限, 品質合計×クラス倍率)
        // Per-step promotion chance u = min(cap, quality sum x class multiplier)
        public static double PromotionChance(double qualityEffectSum, double classQualityMultiplier, double maxPromotionChance)
        {
            return Math.Clamp(qualityEffectSum * classQualityMultiplier, 0d, maxPromotionChance);
        }

        // クラス基準率を品質合計で割り引く。割引の上限は maxReduction
        // Discount a class base rate by the quality sum, capped at maxReduction
        public static double ReducedRate(double classBaseRate, double qualityEffectSum, double maxReduction)
        {
            return classBaseRate * (1d - Math.Clamp(qualityEffectSum, 0d, maxReduction));
        }
    }
}
