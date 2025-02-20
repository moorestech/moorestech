using System;

namespace Game.Fluid
{
    public struct FluidStack
    {
        public readonly Guid FluidId;
        public float Amount;
        public FluidContainer TargetContainer;
        
        public FluidStack(Guid fluidId, float amount, FluidContainer targetContainer)
        {
            FluidId = fluidId;
            Amount = amount;
            TargetContainer = targetContainer;
        }
    }
}