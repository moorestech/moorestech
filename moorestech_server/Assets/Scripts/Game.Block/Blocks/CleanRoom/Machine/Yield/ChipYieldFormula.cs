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
        // 品質の効き（効果合計×部屋倍率）から、十分入れたときの表への近づき度 0〜1 を出す。1個ごとに必ず増え、増え方は逓減する
        // Approach weight 0..1 toward the full-module table from the quality sum times the class multiplier; it always rises per module with diminishing gains
        public static double ApproachWeight(double qualityEffectSum, double classQualityMultiplier, double qualityApproachScale)
        {
            var effectiveQuality = Math.Max(0d, qualityEffectSum * classQualityMultiplier);
            return 1d - Math.Exp(-effectiveQuality / qualityApproachScale);
        }

        // モジュール無しの表と最高Tierの表を表の重みで線形に混ぜ、生き残り1個あたりのレベル別割合と失敗ダイ割合にする
        // Linearly mix the no-module and highest-tier tables by the table weight into per-survivor level rates and the failed-die rate
        private static double[] BlendedDieRates(IReadOnlyList<float> noModuleLevelRates, float noModuleFailedRate, IReadOnlyList<float> tierLevelRates, float tierFailedRate, double tableWeight, out double blendedFailedRate)
        {
            var levelCount = tierLevelRates.Count;
            var blended = new double[levelCount];
            for (var i = 0; i < levelCount; i++) blended[i] = noModuleLevelRates[i] * (1d - tableWeight) + tierLevelRates[i] * tableWeight;
            blendedFailedRate = noModuleFailedRate * (1d - tableWeight) + tierFailedRate * tableWeight;
            return blended;
        }

        // ダイ1個あたりで「無し」と「十分入れた」状態を近づき度で混ぜ、損失率と生き残り1個あたりの振り分けを1回で出す
        // Mix the no-module and full-module states per die by the approach weight, returning the loss rate and per-survivor split in one call
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
            return BlendedDieRates(noModuleLevelRates, noModuleFailedRate, tierLevelRates, tierFailedRate, tableWeight, out failedRate);
        }

        // クラス基準率を品質合計で割り引く。割引の上限は maxReduction（EUV露光の層失敗率に使う）
        // Discount a class base rate by the quality sum, capped at maxReduction (used for the EUV layer fail rate)
        public static double ReducedRate(double classBaseRate, double qualityEffectSum, double maxReduction)
        {
            return classBaseRate * (1d - Math.Clamp(qualityEffectSum, 0d, maxReduction));
        }
    }
}
