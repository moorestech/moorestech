namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     速度ベース流体シミュレーションの調整定数。量・速度は FluidUnits の整数単位で、係数は整数比で持つ(ADR 0090)。
    ///     速度の単位は「量/tick」。面の流量は毎tick、速度をそのまま流す量として解釈される。
    ///
    ///     Tuning constants for the velocity-based fluid simulation. Amounts and velocities are FluidUnits integers and the coefficients are integer ratios (ADR 0090).
    ///     Velocity is measured in amount-per-tick; each tick a face transfers its velocity worth of fluid.
    /// </summary>
    public static class FluidSimulationConstants
    {
        // 圧力ゲイン 0.12 = 12/100: 充填率差1.0×圧力スケール（隣接容量の小さい方）が1tickで生む速度増分の係数
        // リープフロッグの安定条件はおよそ1以下。大きいほど波が速く、小さいほど流れが緩やかになる
        // Pressure gain 0.12 = 12/100: coefficient of per-tick velocity gain from fill-rate difference times pressure scale (min neighbor capacity)
        // Leapfrog stability requires roughly <= 1; higher means faster waves, lower means gentler flow
        public const long PressureGainNumerator = 12;
        public const long PressureGainDenominator = 100;

        // 減衰（粘性）0.002 = 2/1000: 毎tick速度に掛かる摩擦。整数の切り捨てで必ず0へ減衰する
        // Damping (viscosity) 0.002 = 2/1000: per-tick friction on velocity; integer truncation guarantees decay to zero
        public const long DampingPerMille = 2;
    }
}
