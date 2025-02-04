using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Fluid;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        public FluidStack InsertFluidStack(FluidStack fluidStack);
        public bool InsertionCheck(List<FluidStack> fluidStacks);
        
        public FluidStack GetFluidStack(int index);
        void SetFluidStack(int index, FluidStack fluidStack);
        
        public int GetFluidStacksCount();
    }
}