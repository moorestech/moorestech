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
        public readonly List<InContainerFluidStack> FluidStacks = new();
        public Guid FluidId;
        
        /// <param name="capacity">液体の許容量</param>
        /// <param name="fluidId">内部の液体のID</param>
        public FluidContainer(float capacity, Guid fluidId)
        {
            Capacity = capacity;
            FluidId = fluidId;
        }
        
        //TODO: CurrentCapacityをキャッシュする。現時点では実装の簡単のため毎回計算する
        public float TotalAmount => FluidStacks.Sum(f => f.FluidStack.Amount);
        
        /// <summary>
        ///     可能な限り液体を入れる
        /// </summary>
        /// <param name="fluidStack">使用するfluidStack</param>
        /// <param name="remainFluidStack">残ったfluidStack</param>
        public void Fill(FluidStack fluidStack, out FluidStack? remainFluidStack)
        {
            (var stack, FluidStack? remainStack) = Split(fluidStack);
            
            var inContainerFluidStack = new InContainerFluidStack(stack, 0f);
            FluidStacks.Add(inContainerFluidStack);
            
            remainFluidStack = remainStack;
        }
        
        /// <summary>
        ///     移動済みのFluidStackから可能な限り指定された排出量を排出する
        /// </summary>
        /// <param name="maxDrain">最大排出量</param>
        /// <returns>排出したfluidStack</returns>
        public FluidStack Drain(float maxDrain)
        {
            var amount = 0f;
            
            for (var i = FluidStacks.Count - 1; i >= 0; i--)
            {
                var stack = FluidStacks[i];
                
                // 移動量が最大まで達していない場合は排出できない
                if (!stack.IsMoved) continue;
                
                if (stack.FluidStack.Amount < maxDrain - amount)
                {
                    // 余らない場合
                    amount += stack.FluidStack.Amount;
                    FluidStacks.RemoveAt(i);
                }
                else
                {
                    // 余る場合
                    var addingAmount = maxDrain - amount;
                    amount += addingAmount;
                    stack.FluidStack.Amount -= addingAmount;
                    FluidStacks[i] = stack;
                }
                
                // 必要量排出した場合
                if (amount >= maxDrain)
                {
                    break;
                }
            }
            
            return new FluidStack(FluidId, amount, FluidMoveDirection.Forward);
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
    
    public struct InContainerFluidStack
    {
        public FluidStack FluidStack;
        public float MovePercent;
        public bool IsMoved => MovePercent >= 1f;
        
        public InContainerFluidStack(FluidStack fluidStack, float movePercent)
        {
            FluidStack = fluidStack;
            MovePercent = movePercent;
        }
    }
}