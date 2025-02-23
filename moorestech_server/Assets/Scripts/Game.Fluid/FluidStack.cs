using System;

namespace Game.Fluid
{
    public struct FluidStack
    {
        public readonly Guid FluidId;
        public double Amount;
        public FluidContainer TargetContainer;
        public FluidContainer PreviousContainer;
        
        public FluidStack(Guid fluidId, double amount, FluidContainer previousContainer, FluidContainer targetContainer)
        {
            FluidId = fluidId;
            Amount = amount;
            TargetContainer = targetContainer;
            PreviousContainer = previousContainer;
        }
        
        public static (FluidStack stack, FluidStack? remain) Split(FluidStack fluidStack, double amount)
        {
            if (fluidStack.Amount <= amount) return (fluidStack, null);
            
            var remainAmount = fluidStack.Amount - amount;
            var remainFluidStack = new FluidStack(fluidStack.FluidId, remainAmount, fluidStack.PreviousContainer, fluidStack.TargetContainer);
            var newFluidStack = new FluidStack(fluidStack.FluidId, amount, FluidContainer.Empty, FluidContainer.Empty);
            return (newFluidStack, remainFluidStack);
        }
    }
}