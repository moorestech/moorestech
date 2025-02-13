using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Fluid
{
    /// <remarks>
    ///     速度の違うfluidStackはサポート外
    /// </remarks>
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
        
        //TODO: CurrentCapacityをキャッシュする。現時点では実装の簡単のため毎回計算する
        public float TotalAmount => FluidStacks.Sum(f => f.Amount);
        
        /// <param name="fluidStack">使用するfluidStack</param>
        /// <param name="remainFluidStack">残ったfluidStack</param>
        public void Fill(FluidStack fluidStack, out FluidStack? remainFluidStack)
        {
            (var stack, FluidStack? remainStack) = Split(fluidStack);
            
            FluidStacks.Add(stack);
            
            remainFluidStack = remainStack;
        }
        
        public FluidStack Drain(float maxDrain)
        {
            var totalAmount = FluidStacks.Sum(f => f.Amount);
            
            if (totalAmount < maxDrain)
            {
            }
            
            return default;
        }
        
        private (FluidStack stack, FluidStack? remain) Split(FluidStack fluidStack)
        {
            // 容量を超えない場合は分けない
            if (!(TotalAmount + fluidStack.Amount > Capacity)) return (fluidStack, null);
            
            var remainAmount = fluidStack.Amount - (Capacity - TotalAmount);
            var remainFluidStack = new FluidStack(fluidStack.FluidId, remainAmount, fluidStack.FluidMoveDirection);
            var newFluidStack = new FluidStack(fluidStack.FluidId, fluidStack.Amount - remainAmount, fluidStack.FluidMoveDirection);
            return (newFluidStack, remainFluidStack);
        }
    }
}