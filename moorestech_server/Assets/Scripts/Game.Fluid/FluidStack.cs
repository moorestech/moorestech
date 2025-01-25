using System;

namespace Game.Fluid
{
    public struct FluidStack
    {
        public readonly Guid FluidId;
        public readonly float Amount;
        
        public FluidStack(Guid fluidId, float amount)
        {
            FluidId = fluidId;
            Amount = amount;
        }
    }
}