using Game.Block.Interface.Component;
using Game.Fluid;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        public FluidStack? AddLiquid(FluidStack fluidStack, FluidContainer source);
    }
}
