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
        
        public static (FluidStack stack, FluidStack? remain) Split(FluidStack fluidStack, float amount)
        {
            if (fluidStack.Amount <= amount) return (fluidStack, null);
            
            var remainAmount = fluidStack.Amount - amount;
            var remainFluidStack = new FluidStack(fluidStack.FluidId, remainAmount, fluidStack.PreviousContainer, fluidStack.TargetContainer);
            var newFluidStack = new FluidStack(fluidStack.FluidId, fluidStack.Amount - remainAmount, FluidContainer.Empty, FluidContainer.Empty);
            return (newFluidStack, remainFluidStack);
        }
    }
}