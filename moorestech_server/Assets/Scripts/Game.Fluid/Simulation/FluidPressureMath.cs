namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     圧力計算の整数式。Stepper の面・ポートの速度更新と、網の「動けない量」判定(FluidSimNetwork.IsImmovable)が同じ式を共有する。
    ///     量・容量・圧力スケールは FluidUnits の整数(ADR 0090)。容量比の掛け割りは FluidMulDiv が 128 ビットの途中値で行うので、丸めは最後の切り捨て 1 回だけ。
    ///
    ///     Integer pressure formulas shared by the stepper's face and port velocity updates and the network's immovable-amount test (FluidSimNetwork.IsImmovable).
    ///     Amounts, capacities and pressure scales are FluidUnits integers (ADR 0090); capacity-ratio multiply-divides go through FluidMulDiv with a 128-bit intermediate, so the only rounding is one final truncation.
    /// </summary>
    public static class FluidPressureMath
    {
        // 速度 0 から 1 単位の流れを生む最小のゲイン。(0 + gain) * (1000 - 減衰) / 1000 が 1 以上になる最小値で、これ未満は減衰の切り捨てに消えて速度が 0 に張り付く
        // The smallest gain that turns a zero velocity into a one-unit flow: the least value with (0 + gain) * (1000 - damping) / 1000 >= 1; anything smaller vanishes in the damping truncation and the velocity stays at zero
        public const long MinSustainedGain = 2;

        // 静止から動き出せない圧力差の上限(単位)。ゲインが MinSustainedGain に届く最小の圧力差より 1 小さい値(12/100 なら 16)
        // The largest pressure difference (units) that cannot start a flow from rest: one below the smallest difference whose gain reaches MinSustainedGain (16 with 12/100)
        public const long MaxImmovablePressureUnits = (MinSustainedGain * FluidSimulationConstants.PressureGainDenominator + FluidSimulationConstants.PressureGainNumerator - 1) / FluidSimulationConstants.PressureGainNumerator - 1;

        // 両端の充填率差 × 小さい方の容量。容量が等しければ量の差そのもの、違えば大きい側の量を容量比で縮めてから引く(掛け割り 1 回)。A 側が高いと正
        // The fill-rate difference times the smaller capacity: the plain amount difference for equal capacities, else the larger side's amount is scaled by the capacity ratio (one multiply-divide) before subtracting. Positive when side A is higher
        public static long PressureUnits(long amountA, long capacityA, long amountB, long capacityB)
        {
            if (capacityA == capacityB) return amountA - amountB;
            if (capacityA < capacityB) return amountA - FluidMulDiv.MulDiv(amountB, capacityA, capacityB);
            return FluidMulDiv.MulDiv(amountA, capacityB, capacityA) - amountB;
        }

        // 圧力の単位量が 1 tick で生む速度増分
        // Velocity gain one tick of this pressure produces
        public static long Gain(long pressureUnits)
        {
            return pressureUnits * FluidSimulationConstants.PressureGainNumerator / FluidSimulationConstants.PressureGainDenominator;
        }

        // 動けない量: 空の隣へ pressureScale(自分の面のうち最大の min(両端の容量)。自分の容量以下)の面で押しても MinSustainedGain に届かない量(0 を含む)。Stepper が tick の最後に 0 にして名を外す
        // Immovable amount: an amount (zero included) whose push into an empty neighbor over a face of pressureScale (the largest min-capacity among its faces, at most its own capacity) stays below MinSustainedGain; the stepper clears it and its id at the end of a tick
        public static bool IsImmovable(long amount, long capacity, long pressureScale)
        {
            return Gain(PressureUnits(amount, capacity, 0, pressureScale)) < MinSustainedGain;
        }
    }
}
