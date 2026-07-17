namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     隣接ノード間の共有面。速度はこの面が唯一所有する（スタッガード格子）。符号はNodeA→NodeB方向が正。
    ///     NodeAは座標の辞書順で小さい側（正準側）とし、セーブ・クライアント再現時も同一の面が同一の向きを持つ。
    ///
    ///     Shared face between adjacent nodes. The face solely owns its velocity (staggered grid); positive means NodeA→NodeB flow.
    ///     NodeA is the lexicographically-smaller position (canonical side), so saves and client replays agree on face orientation.
    /// </summary>
    public class FluidSimFace
    {
        public readonly FluidSimNode NodeA;
        public readonly FluidSimNode NodeB;

        // この面を1tickに通過できる最大量（両側コネクタのflowCapacityの小さい方×tick秒）
        // Maximum amount this face can pass per tick (min of both connectors' flowCapacity times seconds per tick)
        public readonly double FlowCapacityPerTick;

        // 面速度。単位は量/tick
        // Face velocity in amount-per-tick
        public double Velocity;

        // 流可方向。一方向パイプでは片側の接続しか存在しないため、存在する向きにのみ流せる
        // Allowed flow directions; a one-way pipe has a connection on one side only, so flow is limited to that direction
        public bool AllowAToB;
        public bool AllowBToA;

        // Stepperが1tick内でのみ使う暫定流量
        // Per-tick tentative flux used only inside the stepper
        internal double TentativeFlux;

        public FluidSimFace(FluidSimNode nodeA, FluidSimNode nodeB, double flowCapacityPerTick, double initialVelocity)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            FlowCapacityPerTick = flowCapacityPerTick;
            Velocity = initialVelocity;
        }
    }
}
