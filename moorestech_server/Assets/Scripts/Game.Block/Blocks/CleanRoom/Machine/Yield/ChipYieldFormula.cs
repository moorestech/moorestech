using System;
using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom.Machine.Yield
{
    /// <summary>
    ///     チップ歩留まりの確率式。抽選と機械UIの表示が CleanRoomRoleRates を通して同じ式を使う
    ///     Probability formulas for chip yield; the draws and the machine UI use them through CleanRoomRoleRates
    /// </summary>
    public static class ChipYieldFormula
    {
        // Tier tの表に品質シフトを掛けた、良品ダイ1個あたりのレベル別割合 [Lv1..LvMax] と失敗ダイ割合
        // Per-survivor level rates [Lv1..LvMax] and the failed-die rate after applying the quality shift to tier t's table
        public static double[] ShiftedDieRates(IReadOnlyList<float> levelRates, float failedRate, int tier, double qualityEffectSum, double classQualityMultiplier, double referenceEffectValue, double maxRareShift, out double shiftedFailedRate)
        {
            var rates = ShiftedRatesWithFailedSlot(levelRates, failedRate, tier, qualityEffectSum, classQualityMultiplier, referenceEffectValue, maxRareShift);
            var levelCount = levelRates.Count;
            shiftedFailedRate = rates[levelCount];
            var shiftedLevelRates = new double[levelCount];
            Array.Copy(rates, shiftedLevelRates, levelCount);
            return shiftedLevelRates;
        }

        // 失敗枠を末尾に置いた計算用の配列。シフトの式はこの並びで書く
        // Working array with the failed slot at the end; the shift math is written over this layout
        private static double[] ShiftedRatesWithFailedSlot(IReadOnlyList<float> levelRates, float failedRate, int tier, double qualityEffectSum, double classQualityMultiplier, double referenceEffectValue, double maxRareShift)
        {
            var levelCount = levelRates.Count;
            var rates = new double[levelCount + 1];
            for (var i = 0; i < levelCount; i++) rates[i] = levelRates[i];
            rates[levelCount] = failedRate;

            // レア枠が無いTier（0と最大）はシフトしない
            // Tiers without a shiftable rare slot (0 and the top) do not shift
            if (tier <= 0 || levelCount <= tier) return rates;
            var rareIndex = tier;

            // 失敗枠は Lv1〜Lv(t-1) と失敗ダイ
            // The failure slot is Lv1..Lv(t-1) plus the failed die
            var failureIndices = new int[tier];
            for (var i = 0; i < tier - 1; i++) failureIndices[i] = i;
            failureIndices[tier - 1] = levelCount;
            var failureTotal = 0.0;
            foreach (var index in failureIndices) failureTotal += rates[index];
            var shift = Math.Clamp(qualityEffectSum * classQualityMultiplier - referenceEffectValue, -rates[rareIndex], Math.Min(maxRareShift, failureTotal));

            // 正は失敗枠を比例縮小してレアへ、負はレアから失敗ダイへ
            // Positive shrinks the failure slot proportionally into the rare; negative moves rare into the failed die
            if (0 < shift)
            {
                var keep = (failureTotal - shift) / failureTotal;
                foreach (var index in failureIndices) rates[index] *= keep;
            }
            else rates[levelCount] -= shift;
            rates[rareIndex] += shift;
            return rates;
        }

        // クラス基準率を品質合計で割り引く。割引の上限は maxReduction
        // Discount a class base rate by the quality sum, capped at maxReduction
        public static double ReducedRate(double classBaseRate, double qualityEffectSum, double maxReduction)
        {
            return classBaseRate * (1d - Math.Clamp(qualityEffectSum, 0d, maxReduction));
        }
    }
}
