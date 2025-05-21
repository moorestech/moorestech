using System;
using Core.Master;

namespace Game.Fluid
{
    public readonly struct FluidStack : IEquatable<FluidStack>
    {
        public readonly double Amount;
        public readonly FluidId FluidId;
        
        public FluidStack(double amount, FluidId fluidId)
        {
            Amount = amount;
            FluidId = fluidId;
        }
        
        public bool Equals(FluidStack other)
        {
            return Amount.Equals(other.Amount) && FluidId.Equals(other.FluidId);
        }
        public override bool Equals(object obj)
        {
            return obj is FluidStack other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Amount, FluidId);
        }
    }
}