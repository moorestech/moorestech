using System;

namespace Game.Fluid
{
    public struct FluidStack
    {
        public readonly Guid FluidId;
        public float Amount;
        public FluidContainer FluidContainer;
        public FluidContainer PreviousFluidContainer;
        
        public FluidStack(Guid fluidId, float amount, FluidContainer fluidContainer, FluidContainer previousFluidContainer)
        {
            FluidId = fluidId;
            Amount = amount;
            FluidContainer = fluidContainer;
            PreviousFluidContainer = previousFluidContainer;
        }
    }
}