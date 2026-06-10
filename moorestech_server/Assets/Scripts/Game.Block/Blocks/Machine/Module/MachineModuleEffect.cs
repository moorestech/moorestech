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

        // モジュール未装着時の中立効果（時間1倍・電力1倍・追加出力なし）
        // Neutral effect when no modules are equipped (time x1, power x1, no extra output)
        public static readonly MachineModuleEffect Neutral = new(1f, 1f, 0f);

        public readonly float ProcessingTimeMultiplier;
        public readonly float PowerMultiplier;
        public readonly float ExtraOutputChance;

        private MachineModuleEffect(float processingTimeMultiplier, float powerMultiplier, float extraOutputChance)
        {
            ProcessingTimeMultiplier = processingTimeMultiplier;
            PowerMultiplier = powerMultiplier;
            ExtraOutputChance = extraOutputChance;
        }

        public static MachineModuleEffect Aggregate(IReadOnlyList<ModuleMasterElement> modules)
        {
            // 軸ごとに効果値・トレードオフ値を加算で集計する
            // Sum effect and tradeoff values per axis additively
            var speedSum = 0f;
            var speedTradeoff = 0f;
            var productivitySum = 0f;
            var productivityTradeoff = 0f;
            var efficiencySum = 0f;
            foreach (var module in modules)
            {
                switch (module.EffectAxis)
                {
                    case ModuleMasterElement.EffectAxisConst.Speed:
                        speedSum += module.EffectValue;
                        speedTradeoff += module.TradeoffValue;
                        break;
                    case ModuleMasterElement.EffectAxisConst.Productivity:
                        productivitySum += module.EffectValue;
                        productivityTradeoff += module.TradeoffValue;
                        break;
                    case ModuleMasterElement.EffectAxisConst.Efficiency:
                        efficiencySum += module.EffectValue;
                        break;
                    default:
                        // Quality軸はPhase Bの品質抽選で扱うため、ここでは明示的に無視する
                        // The Quality axis is handled by the Phase B quality roll, so it is explicitly ignored here
                        break;
                }
            }

            // 各係数を計算してクランプする（時間は速度で短縮、電力は省エネで減少、追加出力は0〜1）
            // Compute and clamp each multiplier (speed shortens time, efficiency lowers power, extra output is 0-1)
            var processingTimeMultiplier = (1f + productivityTradeoff) / (1f + speedSum);
            var powerMultiplier = Math.Max((1f + speedTradeoff) / (1f + efficiencySum), MinPowerMultiplier);
            var extraOutputChance = Math.Clamp(productivitySum, 0f, 1f);

            return new MachineModuleEffect(processingTimeMultiplier, powerMultiplier, extraOutputChance);
        }

        public static MachineModuleEffect FromSaved(float powerMultiplier, float extraOutputChance)
        {
            // 加工時間はロード側でtick数として直接復元するため、ここでは1倍とする
            // Processing time is restored directly as ticks on load, so keep its multiplier at 1
            return new MachineModuleEffect(1f, powerMultiplier, extraOutputChance);
        }
    }
}
