namespace Game.Fluid.Sync.State
{
    // 全量に載せる面1枚。両端はノード番号で指し、速度と上限(単位)と許可方向をそのまま写す
    // One face in the full state: both ends as node indices, with its velocity, cap (units) and allowed directions copied as they are
    public readonly struct FluidFaceState
    {
        public readonly int NodeAIndex;
        public readonly int NodeBIndex;
        public readonly long FlowCapacityPerTick;
        public readonly long Velocity;
        public readonly bool AllowAToB;
        public readonly bool AllowBToA;

        public FluidFaceState(int nodeAIndex, int nodeBIndex, long flowCapacityPerTick, long velocity, bool allowAToB, bool allowBToA)
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
