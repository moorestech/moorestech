using System;
using System.Collections.Generic;
using Mooresmaster.Model.ItemsModule;

namespace Game.Block.Blocks.Machine.Module
{
    /// <summary>
    ///     装着モジュール群から機械への効果係数を集計する純粋計算
    ///     Pure aggregation of equipped modules into effect multipliers for a machine
    /// </summary>
    public class MachineModuleEffect
    {
        // 電力倍率の下限（ゼロ消費の防止）
        // Power multiplier floor to prevent zero consumption
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
            // 軸ごとにスタック数加重で加算集計
            // Sum per axis weighted by stack count
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

            // 分母に下限を設け全域で安全にする
            // Floor the denominators to stay safe everywhere
            var speedDenominator = Math.Max(0.01f, 1f + speedSum);
            var efficiencyDenominator = Math.Max(0.01f, 1f + efficiencySum);

            // 各係数を計算してクランプする
            // Compute and clamp each multiplier
            var processingTimeMultiplier = (1f + productivityTradeoff + qualityTradeoff) / speedDenominator;
            var powerMultiplier = Math.Max((1f + speedTradeoff) / efficiencyDenominator, MinPowerMultiplier);
            var extraOutputChance = Math.Clamp(productivitySum, 0f, 1f);

            // 上限は産出側でクランプするため下限のみ
            // Only floor here; the output side clamps the max
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
