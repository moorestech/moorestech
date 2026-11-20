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
        // 品質の効きから近づき度0〜1を算出
        // Approach weight 0..1 toward the full-module table; it always rises per module with diminishing gains
        public static double ApproachWeight(double qualityEffectSum, double classQualityMultiplier, double qualityApproachScale)
        {
            var effectiveQuality = Math.Max(0d, qualityEffectSum * classQualityMultiplier);
            return 1d - Math.Exp(-effectiveQuality / qualityApproachScale);
        }

        // 近づき度で損失率と振り分けを算出
        // Mix no-module and full-module states by the approach weight into the loss rate and per-survivor split
        public static double[] DicingRates(IReadOnlyList<float> noModuleLevelRates, float noModuleFailedRate, IReadOnlyList<float> tierLevelRates, float tierFailedRate, double classDieLossRate, double maxLossReduction, double approachWeight, out double dieLossRate, out double failedRate)
        {
            // 損失率と生き残りはここで1回だけ導出する。十分入れた状態の損失は maxLossReduction だけ減る
            // Derive the loss and survival only here; the full-module state loses maxLossReduction less
            var fullModuleLossRate = classDieLossRate * (1d - maxLossReduction);
            dieLossRate = (1d - approachWeight) * classDieLossRate + approachWeight * fullModuleLossRate;
            var survival = 1d - dieLossRate;
            var fullModuleSurvival = 1d - fullModuleLossRate;

            // 生き残りの内訳で表の重みを決める。良品の合計が近づき度に比例し、1個ごとの伸びが逓減する
            // Weight the tables by where survivors come from, so the good total is linear in the weight and gains diminish per module
            // silent-ok: 全ダイが損失のとき生き残りの割合は抽選に使われない / 損失率1で全数が失敗ダイになり機械UIにもそう出る
            var tableWeight = survival <= 0d ? approachWeight : approachWeight * fullModuleSurvival / survival;
            var blended = BlendedDieRates(out failedRate);
            return blended;

            #region Internal

            // 二つの表を重みで混ぜ、レベル別割合を算出
            // Linearly mix the no-module and highest-tier tables by the table weight into level rates and the failed-die rate
            double[] BlendedDieRates(out double blendedFailedRate)
            {
                var levelCount = tierLevelRates.Count;
                var mixed = new double[levelCount];
                for (var i = 0; i < levelCount; i++) mixed[i] = noModuleLevelRates[i] * (1d - tableWeight) + tierLevelRates[i] * tableWeight;
                blendedFailedRate = noModuleFailedRate * (1d - tableWeight) + tierFailedRate * tableWeight;
                return mixed;
            }

            #endregion
        }

        // クラス基準率を品質合計で割引（上限 maxReduction）
        // Discount a class base rate by the quality sum, capped at maxReduction (used for the EUV layer fail rate)
        public static double ReducedRate(double classBaseRate, double qualityEffectSum, double maxReduction)
        {
            return classBaseRate * (1d - Math.Clamp(qualityEffectSum, 0d, maxReduction));
        }
    }
}
