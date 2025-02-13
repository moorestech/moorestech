using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Fluid
{
    public class FluidContainer
    {
        public readonly float Capacity;
        public readonly List<FluidStack> FluidStacks = new();
        public Guid FluidId;
        
        /// <param name="capacity">液体の許容量</param>
        /// <param name="fluidId">内部の液体のID</param>
        public FluidContainer(float capacity, Guid fluidId)
        {
            Capacity = capacity;
            FluidId = fluidId;
        }
        
        /// <param name="fluidStack">使用するfluidStack</param>
        /// <param name="remainFluidStack">残ったfluidStack</param>
        public void Fill(FluidStack fluidStack, out FluidStack? remainFluidStack)
        {
            (var stack, FluidStack? remainStack) = Split(fluidStack);
            
            FluidStacks.Add(stack);
            
            remainFluidStack = remainStack;
        }
        
        private (FluidStack stack, FluidStack? remain) Split(FluidStack fluidStack)
        {
            //TODO: CurrentCapacityをキャッシュする。現時点では実装の簡単のため毎回計算する
            var currentCapacity = FluidStacks.Sum(f => f.Amount);
            
            // 容量を超えない場合は分けない
            if (!(currentCapacity + fluidStack.Amount > Capacity)) return (fluidStack, null);
            
            
            var remainAmount = fluidStack.Amount - (Capacity - currentCapacity);
            var remainFluidStack = new FluidStack(fluidStack.FluidId, remainAmount);
            var newFluidStack = new FluidStack(fluidStack.FluidId, fluidStack.Amount - remainAmount);
            return (fluidStack, remainFluidStack);
        }
    }
}