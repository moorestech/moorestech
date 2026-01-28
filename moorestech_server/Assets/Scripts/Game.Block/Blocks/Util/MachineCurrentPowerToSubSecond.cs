using System;
using Core.Update;
using Game.EnergySystem;

namespace Game.Block.Blocks.Util
{
    /// <summary>
    ///     機械の現在の電力量から、機械のプロセス（例えば採掘やアイテムの加工など）をどれくらい進めるかを計算する
    ///     Calculate how much to advance the machine process (e.g. mining, item processing) from the current power
    /// </summary>
    public static class MachineCurrentPowerToSubSecond
    {
        private const int RandomSeed = 19890604;
        private static readonly Random SharedRandom = new(RandomSeed);
        // tick数を電力比率で調整して返す（確率的な丸め処理を含む）
        // Return ticks adjusted by power ratio (with probabilistic rounding)
        public static uint GetSubTicks(ElectricPower currentPower, ElectricPower requiredPower)
        {
            // 必要電力が0の時はそのフレームのtick数をそのまま返す
            // When required power is 0, return the full tick count
            if (requiredPower.AsPrimitive() == 0) return GameUpdater.CurrentTickCount;

            // 現在の電力量を必要電力で割った割合でtick数を計算
            // Calculate effective ticks based on power ratio
            var powerRatio = currentPower.AsPrimitive() / (double)requiredPower.AsPrimitive();
            var effectiveTicks = GameUpdater.CurrentTickCount * powerRatio;

            // 整数部と小数部に分離し、小数部は確率的に丸める
            // Split into integer and fractional parts, round fractionally probabilistically
            var wholeTicks = (uint)effectiveTicks;
            var remainder = effectiveTicks - wholeTicks;
            if (SharedRandom.NextDouble() < remainder) wholeTicks++;

            return wholeTicks;
        }
    }
}