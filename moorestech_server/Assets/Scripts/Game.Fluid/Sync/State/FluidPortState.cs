namespace Game.Fluid.Sync.State
{
    // 全量に載せる境界ポート1つ。機械側の実体は持たず、差分がポート番号で渡した量を指すためだけに並びを写す(上限・速度は単位)
    // One boundary port in the full state; it carries no machine, only the ordering that diffs use to address delivered amounts by port index (cap and velocity in units)
    public readonly struct FluidPortState
    {
        public readonly int NodeIndex;
        public readonly long FlowCapacityPerTick;
        public readonly long Velocity;

        public FluidPortState(int nodeIndex, long flowCapacityPerTick, long velocity)
        {
            NodeIndex = nodeIndex;
            FlowCapacityPerTick = flowCapacityPerTick;
            Velocity = velocity;
        }
    }
}
