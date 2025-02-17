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
        public readonly Dictionary<FluidMoveDirection, FluidStack> FluidStacks = new();
        public Guid FluidId;
        
        /// <param name="capacity">液体の許容量</param>
        /// <param name="fluidId">内部の液体のID</param>
        public FluidContainer(float capacity, Guid fluidId)
        {
            Capacity = capacity;
            FluidId = fluidId;
            
            const int fluidMoveDirectionCount = 6;
            for (var i = 0; i < fluidMoveDirectionCount; i++)
            {
                var direction = (FluidMoveDirection)i;
                FluidStacks.Add(direction, new FluidStack(fluidId, 0, direction));
            }
        }
        
        //TODO: CurrentCapacityをキャッシュする。現時点では実装の簡単のため毎回計算する
        public float TotalAmount => FluidStacks.Sum(kvp => kvp.Value.Amount);
        
        /// <summary>
        ///     可能な限り液体を入れる
        /// </summary>
        /// <param name="fluidStack">使用するfluidStack</param>
        /// <param name="remainFluidStack">残ったfluidStack</param>
        public void Fill(FluidStack fluidStack, out FluidStack? remainFluidStack)
        {
            (var addingStack, FluidStack? remainStack) = Split(fluidStack);
            
            var currentStack = FluidStacks[addingStack.FluidMoveDirection];
            currentStack.Amount += addingStack.Amount;
            FluidStacks[addingStack.FluidMoveDirection] = currentStack;
            
            remainFluidStack = remainStack;
        }
        
        /// <summary>
        ///     FluidStackから可能な限り指定された排出量を排出する
        /// </summary>
        /// <param name="maxDrain">最大排出量</param>
        /// <param name="direction">排出方向</param>
        /// <returns>排出したfluidStack</returns>
        public FluidStack Drain(float maxDrain, FluidMoveDirection direction)
        {
            var amount = 0f;
            var currentStack = FluidStacks[direction];
            
            if (currentStack.Amount < maxDrain - amount)
            {
                // 余らない場合
                amount += currentStack.Amount;
                currentStack.Amount = 0;
            }
            else
            {
                // 余る場合
                var addingAmount = maxDrain - amount;
                amount += addingAmount;
                currentStack.Amount -= addingAmount;
            }
            
            FluidStacks[direction] = currentStack;
            
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
}