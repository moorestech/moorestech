namespace Game.Fluid.Sync.State
{
    // 全量に載せる境界ポート1つ。機械側の実体は持たず、差分がポート番号で渡した量を指すためだけに並びを写す
    // One boundary port in the full state; it carries no machine, only the ordering that diffs use to address delivered amounts by port index
    public readonly struct FluidPortState
    {
        public readonly int NodeIndex;
        public readonly double FlowCapacityPerTick;
        public readonly double Velocity;

        public FluidPortState(int nodeIndex, double flowCapacityPerTick, double velocity)
        {
            NodeIndex = nodeIndex;
            FlowCapacityPerTick = flowCapacityPerTick;
            Velocity = velocity;
        }
    }
}
