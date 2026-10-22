namespace Game.Fluid.Sync.State
{
    // 全量に載せる面1枚。両端はノード番号で指し、速度と許可方向をそのまま写す
    // One face in the full state: both ends as node indices, with its velocity and allowed directions copied as they are
    public readonly struct FluidFaceState
    {
        public readonly int NodeAIndex;
        public readonly int NodeBIndex;
        public readonly double FlowCapacityPerTick;
        public readonly double Velocity;
        public readonly bool AllowAToB;
        public readonly bool AllowBToA;

        public FluidFaceState(int nodeAIndex, int nodeBIndex, double flowCapacityPerTick, double velocity, bool allowAToB, bool allowBToA)
        {
            NodeAIndex = nodeAIndex;
            NodeBIndex = nodeBIndex;
            FlowCapacityPerTick = flowCapacityPerTick;
            Velocity = velocity;
            AllowAToB = allowAToB;
            AllowBToA = allowBToA;
        }
    }
}
