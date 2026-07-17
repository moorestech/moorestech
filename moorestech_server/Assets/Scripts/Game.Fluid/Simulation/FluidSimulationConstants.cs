namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     速度ベース流体シミュレーションの調整定数。
    ///     速度の単位は「量/tick」。面の流量は毎tick、速度をそのまま流す量として解釈される。
    ///
    ///     Tuning constants for the velocity-based fluid simulation.
    ///     Velocity is measured in amount-per-tick; each tick a face transfers its velocity worth of fluid.
    /// </summary>
    public static class FluidSimulationConstants
    {
        // 圧力ゲイン: 充填率差1.0×圧力スケール（隣接容量の小さい方）が1tickで生む速度増分の係数
        // リープフロッグの安定条件はおよそ1以下。大きいほど波が速く、小さいほど流れが緩やかになる
        // Pressure gain: coefficient of per-tick velocity gain from fill-rate difference times pressure scale (min neighbor capacity)
        // Leapfrog stability requires roughly <= 1; higher means faster waves, lower means gentler flow
        public const double PressureGain = 0.02;

        // 減衰（粘性）: 毎tick速度に掛かる摩擦。0にするとスロッシングが永久に減衰しなくなる
        // Damping (viscosity): per-tick friction on velocity; at zero, sloshing never settles
        public const double Damping = 0.05;

        // 量ゼロ判定のイプシロン
        // Epsilon for zero-amount checks
        public const double AmountEpsilon = 1e-9;
    }
}
