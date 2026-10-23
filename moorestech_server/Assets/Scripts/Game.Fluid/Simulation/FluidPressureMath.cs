namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     圧力計算の整数式。Stepper の面・ポートの速度更新と、網の痕跡判定(FluidSimNetwork.IsTrace)が同じ式を共有する。
    ///     量・容量・圧力スケールは FluidUnits の整数(ADR 0090)。
    ///
    ///     Integer pressure formulas shared by the stepper's face and port velocity updates and the network's trace test (FluidSimNetwork.IsTrace).
    ///     Amounts, capacities and pressure scales are FluidUnits integers (ADR 0090).
    /// </summary>
    public static class FluidPressureMath
    {
        // 速度 0 から 1 単位の流れを生む最小のゲイン。(0 + gain) * (1000 - 減衰) / 1000 が 1 以上になる最小値で、これ未満は減衰の切り捨てに消えて速度が 0 に張り付く
        // The smallest gain that turns a zero velocity into a one-unit flow: the least value with (0 + gain) * (1000 - damping) / 1000 >= 1; anything smaller vanishes in the damping truncation and the velocity stays at zero
        public const long MinSustainedGain = 2;

        // 整数の充填率。容量 0 は満杯扱い(0 除算を避ける)
        // Integer fill rate; a zero capacity counts as full (avoids division by zero)
        public static long FillRate(long amount, long capacity)
        {
            if (capacity <= 0) return FluidSimulationConstants.FillRateOne;
            return amount * FluidSimulationConstants.FillRateOne / capacity;
        }

        // 充填率差 × 圧力スケール(隣接容量の小さい方)を 1.0 で割った圧力の単位量。64 ビットに収めるため係数を掛ける前に割る
        // Fill-rate difference times the pressure scale (smaller neighbor capacity) divided by 1.0; divided before the gain factor so it fits in 64 bits
        public static long PressureUnits(long fillRateDelta, long pressureScale)
        {
            return fillRateDelta * pressureScale / FluidSimulationConstants.FillRateOne;
        }

        // 圧力の単位量が 1 tick で生む速度増分
        // Velocity gain one tick of this pressure produces
        public static long Gain(long pressureUnits)
        {
            return pressureUnits * FluidSimulationConstants.PressureGainNumerator / FluidSimulationConstants.PressureGainDenominator;
        }

        // 痕跡: 空の隣へ pressureScale の面で押しても MinSustainedGain に届かない量(0 を含む)。流れとして動けないので、別の流体を遮らず、入ってきた流体の名に置き換わる
        // Trace: an amount (zero included) whose push into an empty neighbor over a face of pressureScale stays below MinSustainedGain. It cannot move as a flow, so it neither blocks another fluid nor keeps its id when one arrives
        public static bool IsTrace(long amount, long capacity, long pressureScale)
        {
            return Gain(PressureUnits(FillRate(amount, capacity), pressureScale)) < MinSustainedGain;
        }
    }
}
