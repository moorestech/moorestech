using Game.Fluid;

namespace Game.Block.Blocks.Fluid
{
    internal readonly struct FluidPipeTransferTarget
    {
        public readonly IFluidInventory Inventory;
        public readonly FluidContainer SourceContainer;
        public readonly double MaxFlowAmountPerTick;

        public FluidPipeTransferTarget(IFluidInventory inventory, FluidContainer sourceContainer, double maxFlowAmountPerTick)
        {
            Inventory = inventory;
            SourceContainer = sourceContainer;
            MaxFlowAmountPerTick = maxFlowAmountPerTick;
        }
    }
}
