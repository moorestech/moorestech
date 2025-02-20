using System;

namespace Game.Fluid
{
    public struct FluidStack
    {
        public readonly Guid FluidId;
        public float Amount;
        public FluidContainer TargetContainer;
        public FluidContainer PreviousContainer;
        
        public FluidStack(Guid fluidId, float amount, FluidContainer previousContainer, FluidContainer targetContainer)
        {
            FluidId = fluidId;
            Amount = amount;
            TargetContainer = targetContainer;
            PreviousContainer = previousContainer;
        }
    }
}