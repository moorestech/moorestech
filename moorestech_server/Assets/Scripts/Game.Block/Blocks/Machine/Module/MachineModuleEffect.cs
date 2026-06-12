using System;
using System.Collections.Generic;
using Mooresmaster.Model.ModulesModule;

namespace Game.Block.Blocks.Machine.Module
{
    /// <summary>
    ///     装着モジュール群から機械への効果係数を集計する純粋計算
    ///     Pure aggregation of equipped modules into effect multipliers for a machine
    /// </summary>
    public class MachineModuleEffect
    {
        // 消費電力倍率の下限（省エネモジュールの積みすぎでゼロ消費にならないようにする）
        // Lower bound for the power multiplier (prevents stacking efficiency modules down to zero consumption)
        private const float MinPowerMultiplier = 0.1f;

        public readonly float ProcessingTimeMultiplier;
        public readonly float PowerMultiplier;
        public readonly float ExtraOutputChance;
        public readonly float QualityShift;

        private MachineModuleEffect(float processingTimeMultiplier, float powerMultiplier, float extraOutputChance, float qualityShift)
        {
            ProcessingTimeMultiplier = processingTimeMultiplier;
            PowerMultiplier = powerMultiplier;
            ExtraOutputChance = extraOutputChance;
            QualityShift = qualityShift;
        }

        public static MachineModuleEffect Aggregate(IReadOnlyList<EquippedModule> modules)
        {
            // 軸ごとに効果値・トレードオフ値をスタック数で加重して加算で集計する（UI上の個数と効果を一致させる）
            // Sum effect and tradeoff values per axis additively, weighted by stack count (UI quantity matches the effect)
            var speedSum = 0f;
            var speedTradeoff = 0f;
            var productivitySum = 0f;
            var productivityTradeoff = 0f;
            var efficiencySum = 0f;
            var qualitySum = 0f;
            var qualityTradeoff = 0f;
            foreach (var equipped in modules)
            {
                var module = equipped.Module;
                var count = equipped.Count;
                switch (module.EffectAxis)
                {
                    case ModuleMasterElement.EffectAxisConst.Speed:
                        speedSum += module.EffectValue * count;
                        speedTradeoff += module.TradeoffValue * count;
                        break;
                    case ModuleMasterElement.EffectAxisConst.Productivity:
                        productivitySum += module.EffectValue * count;
                        productivityTradeoff += module.TradeoffValue * count;
                        break;
                    case ModuleMasterElement.EffectAxisConst.Efficiency:
                        efficiencySum += module.EffectValue * count;
                        break;
                    case ModuleMasterElement.EffectAxisConst.Quality:
                        qualitySum += module.EffectValue * count;
                        qualityTradeoff += module.TradeoffValue * count;
                        break;
                    default:
                        break;
                }
            }

            // 分母はマスタ検証で非負が保証されるが、純関数として全域で安全なように下限を設ける
            // Master validation enforces non-negative sums, but floor the denominators so this pure function stays total
            var speedDenominator = Math.Max(0.01f, 1f + speedSum);
            var efficiencyDenominator = Math.Max(0.01f, 1f + efficiencySum);

            // 各係数を計算してクランプする（時間は速度で短縮・生産性/品質トレードオフで延長、電力は省エネで減少、追加出力は0〜1）
            // Compute and clamp each multiplier (speed shortens time, productivity/quality tradeoffs stretch it, efficiency lowers power, extra output is 0-1)
            var processingTimeMultiplier = (1f + productivityTradeoff + qualityTradeoff) / speedDenominator;
            var powerMultiplier = Math.Max((1f + speedTradeoff) / efficiencyDenominator, MinPowerMultiplier);
            var extraOutputChance = Math.Clamp(productivitySum, 0f, 1f);

            // 品質シフトは下限0のみクランプする。上限はレベル数が分かる産出側でクランプされる
            // Quality shift clamps only at zero here; the upper bound is clamped by the output side where the level count is known
            var qualityShift = Math.Max(qualitySum, 0f);

            return new MachineModuleEffect(processingTimeMultiplier, powerMultiplier, extraOutputChance, qualityShift);
        }

        /// <summary>
        ///     装着中モジュール1スロット分（モジュール定義とスタック数）
        ///     One equipped module slot (module definition and stack count)
        /// </summary>
        public readonly struct EquippedModule
        {
            public readonly ModuleMasterElement Module;
            public readonly int Count;

            public EquippedModule(ModuleMasterElement module, int count)
            {
                Module = module;
                Count = count;
            }
        }
    }
}
